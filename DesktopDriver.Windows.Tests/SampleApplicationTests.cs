namespace StockSharp.DesktopDriver.Tests.Windows;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
	// Where the desktop draws the frame of a window, which is where a person sees the window end.
	private const int _extendedFrameBounds = 9;

	[StructLayout(LayoutKind.Sequential)]
	private struct FrameRectangle
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out FrameRectangle value, int size);

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
		await SendAsync(client, apply.Node.Id, new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.None));
		var result = await ReadyAsync(client, "ResultText");
		await WaitAsync(client, result.Node.Id, "state.text", new UiStringValue("Applied: Hello from the runner"));

		var toggle = await ReadyAsync(client, "EnabledToggle");
		await SendAsync(client, toggle.Node.Id, new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.None));
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

		var request = new UiScreenshotRequest(target, UiCaptureKinds.ControlRender, false, 4096, 4096, null, [], []);
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

	/// <summary>
	/// A photographed window ends where its frame does. The rectangle the desktop gives a window takes in the margin it
	/// keeps around the frame to be grabbed by, which nothing draws: a picture of all of it has a black border.
	/// </summary>
	[TestMethod]
	[DataRow("Wpf", "desktopdriver.sample.wpf", "InputText")]
	[DataRow("Avalonia", "desktopdriver.sample", "SymbolBox")]
	[Timeout(180000)]
	public async Task APhotographedWindowEndsAtItsFrame(string toolkit, string appId, string ready)
	{
		await using var app = await StartAsync(toolkit);
		await using var client = await app.ConnectAsync(appId, CancellationToken);
		await ReadyAsync(client, ready);

		var surface = (await client.GetSurfacesAsync(CancellationToken)).Single(item => item.SurfaceId.StartsWith("window:", StringComparison.Ordinal));
		var picture = await client.CaptureScreenshotAsync(
			new UiScreenshotRequest(UiTarget.FromId(surface.Root.Id), UiCaptureKinds.WindowCapture, false, 4096, 4096, null, [], []),
			CancellationToken);

		using var process = Process.GetProcessById(app.Endpoint.ProcessId);

		AreEqual(0, DwmGetWindowAttribute(process.MainWindowHandle, _extendedFrameBounds, out var frame, Marshal.SizeOf<FrameRectangle>()),
			"The desktop would not say where the frame of the window is.");

		AreEqual(frame.Right - frame.Left, picture.PixelWidth, "The picture is not as wide as the frame of the window.");
		AreEqual(frame.Bottom - frame.Top, picture.PixelHeight, "The picture is not as tall as the frame of the window.");
	}

	/// <summary>
	/// The title bar a window draws itself is the window's: its buttons are found among the window's controls, and the
	/// one that closes it closes it - the way a person closes a window that has no button of its own for that.
	/// </summary>
	[TestMethod]
	[Timeout(180000)]
	public async Task TheTitleBarAWindowDrawsItselfIsPartOfTheWindow()
	{
		await using var app = await StartAsync("Avalonia");
		await using var client = await app.ConnectAsync("desktopdriver.sample", CancellationToken);

		var details = await ReadyAsync(client, "DetailsButton");
		await SendAsync(client, details.Node.Id, new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.None));

		// The title bar Avalonia draws names its buttons for automation itself.
		var close = await ReadyAsync(client, "Close");
		AreEqual("window:DetailsWindow", close.Node.Id.ScopeId);

		await SendAsync(client, close.Node.Id, new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.None));

		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);

		while ((await client.GetSurfacesAsync(CancellationToken)).Any(surface => surface.SurfaceId == "window:DetailsWindow"))
		{
			if (DateTime.UtcNow >= deadline)
				Fail("The window is still open after its close button was pressed.");

			await Task.Delay(100, CancellationToken);
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

	/// <summary>
	/// A toolkit whose controls ask the devices themselves which keys are held and which button is down refuses a click
	/// with keys held and a drag, rather than sending a plain click in their place.
	/// </summary>
	/// <remarks>
	/// A click with Ctrl on a chart places an order where a plain one only moves the crosshair; answering one with the
	/// other would make a case pass that never did what it says.
	/// </remarks>
	[TestMethod]
	[DataRow("Wpf", "desktopdriver.sample.wpf")]
	[DataRow("Maui", "desktopdriver.sample.maui.windows")]
	[Timeout(180000)]
	public async Task AClickWithKeysHeldAndADragAreRefusedWhereTheDevicesAreAsked(string toolkit, string appId)
	{
		await using var app = await StartAsync(toolkit);
		await using var client = await app.ConnectAsync(appId, CancellationToken);

		var input = await ReadyAsync(client, "InputText");

		await SendAsync(client, input.Node.Id, new UiTextAction("Typed before", UiTextModes.Replace));

		var apply = await ReadyAsync(client, "ApplyButton");

		var withKeys = await ThrowsAsync<UiAutomationException>(() => client.ExecuteInputAsync(
			new UiInputRequest(Guid.NewGuid(), UiTarget.FromId(apply.Node.Id), UiControlPart.Instance,
				new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.Control), null, TimeSpan.FromSeconds(15)),
			CancellationToken));

		AreEqual(UiErrorCodes.UnsupportedCapability, withKeys.Error.Code);

		var drag = await ThrowsAsync<UiAutomationException>(() => client.ExecuteInputAsync(
			new UiInputRequest(Guid.NewGuid(), UiTarget.FromId(apply.Node.Id), UiControlPart.Instance,
				new UiDragAction(UiControlPart.Instance), null, TimeSpan.FromSeconds(15)),
			CancellationToken));

		AreEqual(UiErrorCodes.UnsupportedCapability, drag.Error.Code);

		// Neither went in as a plain click: the button that applies the text was not pressed.
		var result = await ReadyAsync(client, "ResultText");

		AreNotEqual("Applied: Typed before", ((UiKnown<string>)((BasicState)result.State).Text).Value);
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
