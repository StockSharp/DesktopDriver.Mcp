namespace StockSharp.DesktopDriver.Tests.Protocol;

using System;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia.Threading;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Serialization;

/// <summary>
/// The same questions, asked inside the process and across the channel.
/// </summary>
/// <remarks>
/// There is one implementation of the operations and two doors to it, and the point of these tests is
/// that the door cannot change the answer. They run against a real window from the start, because a
/// module that only worked in the process it was compiled into would automate a library, not an
/// application somebody starts.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class ProtocolParityTests : BaseTestClass
{
	// The body is handed over as a lambda the session can see is asynchronous, not as a delegate: given a
	// delegate the session runs it, takes back the task it returned and never waits on it, so the
	// interface thread stops pumping the moment the body first awaits and a failed assertion is never
	// seen at all.
	private static Task RunAsync(Func<Task> body)
		=> AssemblyInitializer.Session.Dispatch(async () =>
		{
			await body();
			return true;
		}, CancellationToken.None);

	[TestMethod]
	[Timeout(60000)]
	public Task TheSameNodeReadsTheSameThroughTheChannelAsInTheProcess() => RunAsync(async () =>
	{
		await using var hosted = await HostedApplication.StartAsync(CancellationToken.None);
		await using var client = await hosted.ConnectAsync(CancellationToken.None);

		var target = UiTarget.FromId(hosted.ButtonId);

		var inProcess = await hosted.Service.CaptureAsync(target, UiCaptureOptions.Default, CancellationToken.None);
		var overChannel = await client.CaptureAsync(target, UiCaptureOptions.Default, CancellationToken.None);

		AreEqual(inProcess.Node.Id, overChannel.Node.Id);
		AreEqual(inProcess.Node.Kind, overChannel.Node.Kind);
		AreEqual(inProcess.Ready, overChannel.Ready);
		AreEqual(UiJson.Write(inProcess.State), UiJson.Write(overChannel.State));
		AreEqual(UiJson.Write(inProcess.Presentation), UiJson.Write(overChannel.Presentation));

		// Each read is its own read, and says so.
		AreNotEqual(inProcess.Stamp.SnapshotId, overChannel.Stamp.SnapshotId);
		AreEqual(hosted.Session.InstanceId, overChannel.Stamp.InstanceId);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task TheSessionSaysWhichProductAndWhichRunningCopyAnswered() => RunAsync(async () =>
	{
		await using var hosted = await HostedApplication.StartAsync(CancellationToken.None);
		await using var client = await hosted.ConnectAsync(CancellationToken.None);

		var session = await client.GetSessionAsync(CancellationToken.None);

		AreEqual(HostedApplication.AppId, session.AppId);
		AreEqual(hosted.Session.InstanceId, session.InstanceId);
		AreEqual("avalonia.headless", session.InputBackend);
		IsTrue(session.SafeTestProfile);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AClickSentOverTheChannelIsARealClick() => RunAsync(async () =>
	{
		await using var hosted = await HostedApplication.StartAsync(CancellationToken.None);
		await using var client = await hosted.ConnectAsync(CancellationToken.None);

		var receipt = await client.ExecuteInputAsync(
			new UiInputRequest(
				Guid.NewGuid(),
				UiTarget.FromId(hosted.ButtonId),
				UiControlPart.Instance,
				new UiClickAction(UiPointerButtons.Left, 1),
				null,
				TimeSpan.FromSeconds(5)),
			CancellationToken.None);

		Dispatcher.UIThread.RunJobs();

		AreEqual(UiActionStatuses.Dispatched, receipt.Status);
		AreEqual("avalonia.headless", receipt.Backend);
		AreEqual(1, hosted.Clicks, "The click went somewhere other than the button.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task WhatIsOnScreenIsTheSameListOnBothSides() => RunAsync(async () =>
	{
		await using var hosted = await HostedApplication.StartAsync(CancellationToken.None);
		await using var client = await hosted.ConnectAsync(CancellationToken.None);

		var inProcess = await hosted.Service.GetSurfacesAsync(CancellationToken.None);
		var overChannel = await client.GetSurfacesAsync(CancellationToken.None);

		AreEqual(1, overChannel.Length);
		AreEqual(inProcess[0].SurfaceId, overChannel[0].SurfaceId);
		AreEqual(inProcess[0].Root.Id, overChannel[0].Root.Id);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task TheTreeSurvivesTheJourneyWithItsNodesAndItsEdges() => RunAsync(async () =>
	{
		// A tree is the one answer big enough that a channel which got the serialisation wrong would
		// still return something plausible, so it is compared whole rather than sampled.
		await using var hosted = await HostedApplication.StartAsync(CancellationToken.None);
		await using var client = await hosted.ConnectAsync(CancellationToken.None);

		var query = new UiTreeQuery(null, false, UiCaptureOptions.Default);

		var inProcess = await hosted.Service.GetTreeAsync(query, CancellationToken.None);
		var overChannel = await client.GetTreeAsync(query, CancellationToken.None);

		AreEqual(inProcess.Nodes.Length, overChannel.Nodes.Length);
		AreEqual(inProcess.Links.Length, overChannel.Links.Length);
		IsTrue(
			overChannel.Nodes.Length >= 2,
			$"The window and its button should both be there; got {overChannel.Nodes.Length}.");

		for (var index = 0; index < inProcess.Nodes.Length; index++)
		{
			AreEqual(inProcess.Nodes[index].Node.Id, overChannel.Nodes[index].Node.Id);
			AreEqual(UiJson.Write(inProcess.Nodes[index].State), UiJson.Write(overChannel.Nodes[index].State));
		}
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AnAddressThatNamesNothingIsRefusedRatherThanAnswered() => RunAsync(async () =>
	{
		await using var hosted = await HostedApplication.StartAsync(CancellationToken.None);
		await using var client = await hosted.ConnectAsync(CancellationToken.None);

		var error = await ThrowsAsync<UiAutomationException>(() => client.CaptureAsync(
			UiTarget.FromId(new UiNodeId(hosted.ButtonId.ScopeId, "NoSuchControl")),
			UiCaptureOptions.Default,
			CancellationToken.None));

		AreEqual(UiErrorCodes.NotFound, error.Error.Code);
	});
}
