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
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// A run that may not be driven is not driven through a control that performs its own input either.
/// </summary>
/// <remarks>
/// A ribbon, an editor or a picker can carry an action out through its own automation, without the input
/// backend. The backend is where a run on a live profile refuses input, so an action has to be refused
/// before such a control is offered it - or anything that could reach the endpoint could press the ribbon
/// of a product on a live account.
/// </remarks>
[TestClass]
public class InputRefusalTests : BaseTestClass
{
	private static readonly UiNodeId _id = new("window:test", "ribbon");

	[TestMethod]
	public async Task AControlThatPerformsItsOwnInputIsNotOfferedAnActionTheRunRefuses()
	{
		var adapter = new PerformingAdapter();
		var dispatcher = Create(adapter, new UiNoInputDriver("Input is delivered only to a run started on a test profile."));

		var error = await ThrowsAsync<UiAutomationException>(() => dispatcher.ExecuteAsync(Click(), CancellationToken));

		AreEqual(UiErrorCodes.InputUnavailable, error.Error.Code);
		AreEqual(0, adapter.Performed, "The control carried out an action the run refuses.");
	}

	[TestMethod]
	public async Task AControlThatPerformsItsOwnInputTakesAnActionTheRunAllows()
	{
		var adapter = new PerformingAdapter();
		var driver = new SilentDriver();
		var dispatcher = Create(adapter, driver);

		var receipt = await dispatcher.ExecuteAsync(Click(), CancellationToken);

		AreEqual(UiActionStatuses.Dispatched, receipt.Status);
		AreEqual(1, adapter.Performed);
		AreEqual(0, driver.Sent, "The backend sent what the control had already carried out.");
	}

	private static UiInputRequest Click()
		=> new(
			Guid.NewGuid(),
			UiTarget.FromId(_id),
			UiControlPart.Instance,
			new UiClickAction(UiPointerButtons.Left, 1),
			null,
			TimeSpan.FromSeconds(5));

	private static UiInputDispatcher Create(PerformingAdapter adapter, IUiInputDriver driver)
	{
		var nodes = new UiNodeRegistry(Guid.NewGuid());
		var adapters = new UiAdapterRegistry();
		var revisions = new UiRevisionTracker();

		nodes.Register(new UiSubject(_id, new TestControl()), "ribbon", true);
		adapters.Register("stub", typeof(TestControl), adapter);

		return new UiInputDispatcher(
			nodes,
			adapters,
			revisions,
			new CentreResolver(nodes, revisions),
			driver,
			new ImmediateExecutor(),
			new UiActionJournal());
	}

	private sealed class PerformingAdapter() : StubAdapter("ribbon", typeof(TestControl)), IUiInputPerformingAdapter
	{
		public int Performed { get; private set; }

		public bool CanPerform(UiSubject subject, UiTargetPart part, UiInputAction action) => true;

		public void Perform(UiSubject subject, UiTargetPart part, UiInputAction action) => Performed++;
	}

	private sealed class CentreResolver(UiNodeRegistry nodes, UiRevisionTracker revisions) : IUiInputTargetResolver
	{
		public UiResolvedInputTarget Resolve(UiSubject subject, UiTargetPart part, UiInputAction action, UiCaptureContext context)
			=> new(nodes.GetReference(subject.Id), "window:test", new UiRect(0, 0, 10, 10), new UiPoint(5, 5), revisions.Read(subject.Id));
	}

	private sealed class SilentDriver : IUiInputDriver
	{
		public int Sent { get; private set; }

		public string BackendId => "test";

		public ImmutableArray<string> Capabilities { get; } = ["input.click"];

		public UiError Refusal => null;

		public Task<UiActionReceipt> ExecuteAsync(UiInputRequest request, UiResolvedInputTarget resolvedTarget, CancellationToken cancellationToken)
		{
			Sent++;

			return Task.FromResult(new UiActionReceipt(
				request.ActionId,
				UiActionStatuses.Dispatched,
				BackendId,
				UiField<UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
				UiField<UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
				true,
				null));
		}
	}
}
