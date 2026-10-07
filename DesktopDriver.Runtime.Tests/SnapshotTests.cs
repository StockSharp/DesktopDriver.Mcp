namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;
using StockSharp.DesktopDriver.Waiting;

/// <summary>
/// What one read of one node returns, and when it refuses.
/// </summary>
[TestClass]
public class SnapshotTests : BaseTestClass
{
	private static readonly UiNodeId _id = new("panel:orders", "orders-grid");

	private static (UiSnapshotService Service, UiNodeRegistry Nodes, UiAdapterRegistry Adapters, UiRevisionTracker Revisions)
		Create(object control, IUiSnapshotAdapter adapter)
	{
		var nodes = new UiNodeRegistry(Guid.NewGuid());
		var adapters = new UiAdapterRegistry();
		var revisions = new UiRevisionTracker();

		nodes.Register(new UiSubject(_id, control), "grid", true);

		if (adapter is not null)
			adapters.Register("stub", control.GetType(), adapter);

		return (
			new UiSnapshotService(Guid.NewGuid(), nodes, adapters, revisions, new StubPresentationReader(), new ImmediateExecutor()),
			nodes,
			adapters,
			revisions);
	}

	[TestMethod]
	public async Task AReadWithoutAGuardIsOneLookAtTheInterface()
	{
		var (service, _, _, _) = Create(new TestControl(), new StubAdapter("grid", typeof(TestControl)));

		var snapshot = await service.CaptureAsync(UiTarget.FromId(_id), UiCaptureOptions.Default, CancellationToken.None);

		AreEqual(UiConsistencies.UiThreadRead, snapshot.Stamp.Consistency);
		AreEqual("stub", ((BasicState)snapshot.State).Text is UiKnown<string> text ? text.Value : null);
	}

	[TestMethod]
	public async Task AReadUnderAGuardSaysItWasChecked()
	{
		var (service, _, _, revisions) = Create(new TestControl(), new StubAdapter("grid", typeof(TestControl)));
		var current = revisions.Read(_id);

		var snapshot = await service.CaptureAsync(
			UiTarget.FromId(_id),
			UiCaptureOptions.Default with { Guard = new UiReadGuard(null, current.View, null) },
			CancellationToken.None);

		AreEqual(UiConsistencies.VersionChecked, snapshot.Stamp.Consistency);
	}

	[TestMethod]
	public async Task AReadIsRefusedWhenWhatTheCallerSawHasMovedOn()
	{
		// This is what keeps a second page of rows from being half of one sort and half of another.
		var (service, _, _, revisions) = Create(new TestControl(), new StubAdapter("grid", typeof(TestControl)));
		var stale = revisions.Read(_id).View;

		revisions.Bump(_id, UiRevisionKinds.View);

		var error = await ThrowsAsync<UiAutomationException>(() => service.CaptureAsync(
			UiTarget.FromId(_id),
			UiCaptureOptions.Default with { Guard = new UiReadGuard(null, stale, null) },
			CancellationToken.None));

		AreEqual(UiErrorCodes.StateChanged, error.Error.Code);
	}

	[TestMethod]
	public async Task AControlNobodyWroteAnAdapterForIsStillANode()
	{
		var (service, _, _, _) = Create(new TestControl(), null);

		var snapshot = await service.CaptureAsync(UiTarget.FromId(_id), UiCaptureOptions.Default, CancellationToken.None);

		AreEqual(UiReadyStatuses.Unknown, snapshot.Ready);
		IsTrue(snapshot.Completeness.IsPartial, "A basic answer is a partial answer and says so.");
		AreEqual(1, snapshot.Warnings.Length);
		IsTrue(snapshot.Presentation.IsEffectivelyVisible is UiKnown<bool> { Value: true },
			"Where it is and whether it is visible are known even without an adapter.");
	}

	[TestMethod]
	public async Task TheCapabilitiesComeFromTheAdapterThatAnswered()
	{
		var adapter = new StubAdapter("grid", typeof(TestControl)) { Capabilities = ["grid.rows", "input.click"] };
		var (service, _, _, _) = Create(new TestControl(), adapter);

		var snapshot = await service.CaptureAsync(UiTarget.FromId(_id), UiCaptureOptions.Default, CancellationToken.None);

		AreEqual(2, snapshot.Capabilities.Length);
		AreEqual("grid.rows", snapshot.Capabilities[0]);
	}
}
