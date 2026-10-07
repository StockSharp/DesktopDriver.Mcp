namespace StockSharp.DesktopDriver.Tests.Windows;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Client;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Protocol;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runner;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;
using StockSharp.DesktopDriver.Waiting;

// These tests drive separate applications. MAUI controls have their real Windows handlers, rather
// than the handler-free controls used by DesktopDriver.Maui.Tests.
[TestClass]
[DoNotParallelize]
public class SampleApplicationTests : BaseTestClass
{
	[TestMethod]
	[DataRow("Wpf", "desktopdriver.sample.wpf", "wpf.synthetic")]
	[DataRow("Maui", "desktopdriver.sample.maui.windows", "maui.windows")]
	[Timeout(180000)]
	public async Task ARunnerReadsControlsAndSendsInput(string toolkit, string appId, string backend)
	{
		await using var app = await StartAsync(toolkit);
		await using var client = await app.ConnectAsync(appId, CancellationToken);

		AreEqual(app.Endpoint.InstanceId, client.Session.InstanceId);
		AreEqual(backend, client.Session.InputBackend);
		IsTrue(client.Session.SafeTestProfile);

		var input = await ReadyAsync(client, "InputText");
		AreEqual(new UiNodeId("window:MainWindow", "InputText"), input.Node.Id);
		IsTrue(((UiKnown<bool>)input.Presentation.IsAttached).Value);
		IsTrue(((UiKnown<UiRect>)input.Presentation.BoundsInSurfaceDip).Value.Width > 0);

		var items = await ReadyAsync(client, "ItemList");
		AreEqual(3L, ((UiKnown<long>)((ListState)items.State).ItemCount).Value);

		await SendAsync(client, input.Node.Id, new UiTextAction("Hello from the runner", UiTextModes.Replace));
		await WaitAsync(client, input.Node.Id, "state.text", new UiStringValue("Hello from the runner"));

		var apply = await ReadyAsync(client, "ApplyButton");
		await SendAsync(client, apply.Node.Id, new UiClickAction(UiPointerButtons.Left, 1));
		var result = await ReadyAsync(client, "ResultText");
		await WaitAsync(client, result.Node.Id, "state.text", new UiStringValue("Applied: Hello from the runner"));

		var toggle = await ReadyAsync(client, "EnabledToggle");
		await SendAsync(client, toggle.Node.Id, new UiClickAction(UiPointerButtons.Left, 1));
		await WaitAsync(client, toggle.Node.Id, "state.isChecked", new UiBooleanValue(true));
	}

	[TestMethod]
	[DataRow("Wpf", "desktopdriver.sample.wpf")]
	[DataRow("Maui", "desktopdriver.sample.maui.windows")]
	[Timeout(180000)]
	public async Task ARunnerReceivesAPngAndTheRequestedCaptureKind(string toolkit, string appId)
	{
		await using var app = await StartAsync(toolkit);
		await using var client = await app.ConnectAsync(appId, CancellationToken);
		await ReadyAsync(client, "InputText");
		var surface = (await client.GetSurfacesAsync(CancellationToken)).Single(item => item.SurfaceId == "window:MainWindow");
		var target = UiTarget.FromId(surface.Root.Id);

		var request = new UiScreenshotRequest(target, UiCaptureKinds.ControlRender, false, 4096, 4096, null, []);
		var picture = await client.CaptureScreenshotAsync(request, CancellationToken);
		await AssertPngAsync(client, picture, UiCaptureKinds.ControlRender);

		if (toolkit == "Wpf")
		{
			var photographed = await client.CaptureScreenshotAsync(request with { CaptureKind = UiCaptureKinds.WindowCapture }, CancellationToken);
			await AssertPngAsync(client, photographed, UiCaptureKinds.WindowCapture);
		}
		else
		{
			try
			{
				await client.CaptureScreenshotAsync(request with { CaptureKind = UiCaptureKinds.WindowCapture }, CancellationToken);
				Assert.Fail("MAUI must refuse window capture rather than replace it with a control render.");
			}
			catch (UiAutomationException error)
			{
				AreEqual(UiErrorCodes.UnsupportedCapability, error.Error.Code);
			}
		}
	}

	[TestMethod]
	[DataRow("Wpf")]
	[DataRow("Maui")]
	[Timeout(180000)]
	public async Task AnOrdinaryLaunchDoesNotPublishAnEndpoint(string toolkit)
	{
		var executable = Executable(toolkit);
		var endpointFile = Path.Combine(Path.GetDirectoryName(executable), $"disabled.{Guid.NewGuid():N}.json");
		var start = new ProcessStartInfo(executable)
		{
			UseShellExecute = false,
			RedirectStandardError = true,
			WorkingDirectory = Path.GetDirectoryName(executable),
		};

		// An endpoint argument alone must not enable automation.
		start.ArgumentList.Add(UiLaunchProtocol.EndpointSwitch + endpointFile);
		using var process = Process.Start(start);

		try
		{
			var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);

			while (process.MainWindowHandle == IntPtr.Zero)
			{
				if (process.HasExited)
					Assert.Fail($"{toolkit} exited with code {process.ExitCode}: {await process.StandardError.ReadToEndAsync(CancellationToken)}");

				if (DateTime.UtcNow >= deadline)
					Assert.Fail($"{toolkit} did not show its window.");

				await Task.Delay(100, CancellationToken);
				process.Refresh();
			}

			await Task.Delay(300, CancellationToken);
			IsFalse(File.Exists(endpointFile), "An ordinary launch opened an automation endpoint.");
		}
		finally
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
				await process.WaitForExitAsync();
			}

			File.Delete(endpointFile);
		}
	}

	private Task<DrivenApplication> StartAsync(string toolkit)
		=> DrivenApplication.StartAsync(Executable(toolkit), null, TimeSpan.FromSeconds(90), CancellationToken);

	private static string Executable(string toolkit)
		=> DrivenApplication.Executable(typeof(SampleApplicationTests).Assembly, toolkit + "Executable");

	private async Task<UiNodeSnapshot> ReadyAsync(UiAutomationClient client, string id)
	{
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);

		while (true)
		{
			var found = await client.FindAsync(new UiFindQuery(UiSelector.Any with { AutomationId = id }, new UiPageRequest(2, null)), CancellationToken);
			AreEqual(1, found.Items.Length, $"Expected one control named {id}.");
			var target = UiTarget.FromId(found.Items[0].Id);
			var snapshot = await client.CaptureAsync(target, UiCaptureOptions.Default, CancellationToken);

			if (snapshot.Presentation.BoundsInSurfaceDip is UiKnown<UiRect> { Value.Width: > 0, Value.Height: > 0 })
				return snapshot;

			if (DateTime.UtcNow >= deadline)
				Assert.Fail($"{id} was not laid out.");

			await Task.Delay(100, CancellationToken);
		}
	}

	private async Task SendAsync(UiAutomationClient client, UiNodeId node, UiInputAction action)
	{
		var receipt = await client.ExecuteInputAsync(new UiInputRequest(Guid.NewGuid(), UiTarget.FromId(node),
			UiControlPart.Instance, action, null, TimeSpan.FromSeconds(15)), CancellationToken);

		IsNull(receipt.Error, receipt.Error?.Message);
		IsTrue(receipt.AnyInputDispatched);
	}

	private Task<UiWaitResult> WaitAsync(UiAutomationClient client, UiNodeId node, string path, UiValue expected)
		=> client.WaitAsync(new UiWaitRequest(UiTarget.FromId(node), new UiFieldCondition(path, UiComparisons.Equal, expected),
			TimeSpan.FromSeconds(15), UiCaptureOptions.Default), CancellationToken);

	private async Task AssertPngAsync(UiAutomationClient client, UiScreenshotInfo picture, string kind)
	{
		AreEqual(kind, picture.CaptureKind);
		AreEqual("image/png", picture.MimeType);
		IsTrue(picture.PixelWidth > 0 && picture.PixelHeight > 0);
		IsTrue(picture.ByteLength > 8);
		var chunk = await client.ReadArtifactAsync(new UiArtifactReadRequest(picture.ArtifactId, 0, 8), CancellationToken);
		IsTrue(chunk.Data.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "The artifact is not a PNG.");
	}
}
