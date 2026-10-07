namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Values;
using StockSharp.DesktopDriver.Waiting;

/// <summary>
/// Waiting for the interface to become what the test expects.
/// </summary>
[TestClass]
public class WaitTests : BaseTestClass
{
	private static readonly UiNodeId _id = new("panel:orders", "orders-grid");

	private sealed class Fixture
	{
		public UiNodeRegistry Nodes { get; } = new(Guid.NewGuid());
		public UiAdapterRegistry Adapters { get; } = new();
		public UiRevisionTracker Revisions { get; } = new();
		public StubAdapter Adapter { get; } = new("grid", typeof(TestControl));
		public UiWaitService Waits { get; }

		public Fixture(bool register = true)
		{
			if (register)
			{
				Nodes.Register(new UiSubject(_id, new TestControl()), "grid", true);
				Adapters.Register("stub", typeof(TestControl), Adapter);
			}

			var snapshots = new UiSnapshotService(
				Guid.NewGuid(), Nodes, Adapters, Revisions, new StubPresentationReader(), new ImmediateExecutor());

			Waits = new UiWaitService(snapshots, Revisions);
		}
	}

	[TestMethod]
	[Timeout(20000)]
	public async Task AConditionThatAlreadyHoldsFinishesAtOnce()
	{
		var fixture = new Fixture();

		var result = await fixture.Waits.WaitAsync(
			new UiWaitRequest(
				UiTarget.FromId(_id),
				new UiFieldCondition("state.text", UiComparisons.Equal, new UiStringValue("stub")),
				TimeSpan.FromSeconds(5),
				UiCaptureOptions.Default),
			CancellationToken.None);

		AreEqual(1, result.ObservationCount);
		IsNotNull(result.LastSnapshot);
	}

	[TestMethod]
	[Timeout(20000)]
	public async Task AConditionThatBecomesTrueIsNoticed()
	{
		var fixture = new Fixture();

		var waiting = fixture.Waits.WaitAsync(
			new UiWaitRequest(
				UiTarget.FromId(_id),
				new UiFieldCondition("state.text", UiComparisons.Equal, new UiStringValue("done")),
				TimeSpan.FromSeconds(10),
				UiCaptureOptions.Default),
			CancellationToken.None);

		await Task.Delay(100);
		fixture.Adapter.Text = "done";
		fixture.Revisions.Bump(_id, UiRevisionKinds.State);

		var result = await waiting;

		IsTrue(result.ObservationCount >= 2);
	}

	[TestMethod]
	[Timeout(20000)]
	public async Task AConditionThatNeverHoldsRunsOutOfTimeAndSaysWhatItSaw()
	{
		var fixture = new Fixture();

		var error = await ThrowsAsync<UiAutomationException>(() => fixture.Waits.WaitAsync(
			new UiWaitRequest(
				UiTarget.FromId(_id),
				new UiFieldCondition("state.text", UiComparisons.Equal, new UiStringValue("never")),
				TimeSpan.FromMilliseconds(300),
				UiCaptureOptions.Default),
			CancellationToken.None));

		AreEqual(UiErrorCodes.Timeout, error.Error.Code);
		IsTrue(error.Error.Details.ContainsKey("observations"), "A timeout says how many times it looked.");
		IsTrue(error.Error.Details.ContainsKey("elapsedMs"));
	}

	[TestMethod]
	[Timeout(20000)]
	public async Task WaitingForSomethingToBeGoneWorksOnAnAddressThatIsNotRegistered()
	{
		var fixture = new Fixture(register: false);

		var result = await fixture.Waits.WaitAsync(
			new UiWaitRequest(
				UiTarget.FromId(_id),
				new UiExistsCondition(false),
				TimeSpan.FromSeconds(5),
				UiCaptureOptions.Default),
			CancellationToken.None);

		IsNull(result.LastSnapshot, "There is nothing to describe, which is what was being waited for.");
	}

	[TestMethod]
	[Timeout(20000)]
	public async Task APathTheSchemaHasNoRootForIsAMistakeInTheRequest()
	{
		var fixture = new Fixture();

		var error = await ThrowsAsync<UiAutomationException>(() => fixture.Waits.WaitAsync(
			new UiWaitRequest(
				UiTarget.FromId(_id),
				new UiFieldCondition("dataContext.secret", UiComparisons.Equal, new UiStringValue("x")),
				TimeSpan.FromSeconds(1),
				UiCaptureOptions.Default),
			CancellationToken.None));

		AreEqual(UiErrorCodes.InvalidRequest, error.Error.Code);
	}

	[TestMethod]
	[Timeout(20000)]
	public async Task AConditionWithTooManyTermsIsRefused()
	{
		var fixture = new Fixture();
		var terms = ImmutableArray.CreateBuilder<UiCondition>();

		for (var index = 0; index < 20; index++)
			terms.Add(new UiExistsCondition(true));

		var error = await ThrowsAsync<UiAutomationException>(() => fixture.Waits.WaitAsync(
			new UiWaitRequest(
				UiTarget.FromId(_id),
				new UiAllCondition(terms.ToImmutable()),
				TimeSpan.FromSeconds(1),
				UiCaptureOptions.Default),
			CancellationToken.None));

		AreEqual(UiErrorCodes.InvalidRequest, error.Error.Code);
	}

	[TestMethod]
	[Timeout(20000)]
	public async Task WaitingLongerThanTheProtocolAllowsIsRefused()
	{
		var fixture = new Fixture();

		var error = await ThrowsAsync<UiAutomationException>(() => fixture.Waits.WaitAsync(
			new UiWaitRequest(
				UiTarget.FromId(_id),
				new UiExistsCondition(true),
				TimeSpan.FromMinutes(10),
				UiCaptureOptions.Default),
			CancellationToken.None));

		AreEqual(UiErrorCodes.InvalidRequest, error.Error.Code);
	}

	[TestMethod]
	[Timeout(20000)]
	public async Task TheCallerCanStopWaiting()
	{
		var fixture = new Fixture();
		using var cancellation = new CancellationTokenSource();

		var waiting = fixture.Waits.WaitAsync(
			new UiWaitRequest(
				UiTarget.FromId(_id),
				new UiFieldCondition("state.text", UiComparisons.Equal, new UiStringValue("never")),
				TimeSpan.FromSeconds(30),
				UiCaptureOptions.Default),
			cancellation.Token);

		await Task.Delay(100);
		cancellation.Cancel();

		await ThrowsAsync<OperationCanceledException>(() => waiting);
	}

	[TestMethod]
	[Timeout(20000)]
	public async Task AWaitForANewerViewFinishesWhenTheViewMoves()
	{
		var fixture = new Fixture();
		var baseline = fixture.Revisions.Read(_id).View;

		var waiting = fixture.Waits.WaitAsync(
			new UiWaitRequest(
				UiTarget.FromId(_id),
				new UiRevisionAfterCondition(UiRevisionKinds.View, baseline),
				TimeSpan.FromSeconds(10),
				UiCaptureOptions.Default),
			CancellationToken.None);

		await Task.Delay(100);
		fixture.Revisions.Bump(_id, UiRevisionKinds.View);

		IsNotNull((await waiting).LastSnapshot);
	}
}
