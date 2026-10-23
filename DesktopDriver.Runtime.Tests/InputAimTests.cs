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
/// An action is aimed only once the window has drawn what it has laid out.
/// </summary>
/// <remarks>
/// Where an action lands is worked out from where the controls are laid out, and the window delivers it to what it has
/// drawn there. A control shown, moved or uncovered a moment ago is laid out but not on the screen yet, and an action
/// aimed at it then reaches whatever was drawn in its place before - as a person never does, who presses only what they
/// can see.
/// </remarks>
[TestClass]
public class InputAimTests : BaseTestClass
{
	private static readonly UiNodeId _id = new("window:test", "button");

	[TestMethod]
	public async Task AnActionIsAimedOnlyOnceTheWindowHasDrawnWhatItLaidOut()
	{
		var drawn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var driver = new DrawingDriver(drawn.Task);
		var resolver = new CountingResolver();
		var dispatcher = Create(resolver, driver);

		var executing = dispatcher.ExecuteAsync(Click(), CancellationToken);

		AreEqual(0, resolver.Resolved, "The action was aimed before the window had drawn what it laid out.");
		IsFalse(executing.IsCompleted, "The action went out before the window had drawn what it laid out.");
		AreEqual(_id, driver.WaitedFor?.Id, "The wait was not for the window of the action's target.");

		drawn.SetResult();

		var receipt = await executing;

		AreEqual(UiActionStatuses.Dispatched, receipt.Status);
		AreEqual(1, resolver.Resolved);
		AreEqual(1, driver.Sent);
	}

	[TestMethod]
	public async Task AnActionCancelledWhileTheWindowDrawsIsNeitherAimedNorSent()
	{
		var driver = new DrawingDriver(new TaskCompletionSource().Task);
		var resolver = new CountingResolver();
		var dispatcher = Create(resolver, driver);

		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

		var executing = dispatcher.ExecuteAsync(Click(), cancellation.Token);

		cancellation.Cancel();

		await ThrowsAsync<OperationCanceledException>(() => executing);

		AreEqual(0, resolver.Resolved, "A cancelled action was aimed.");
		AreEqual(0, driver.Sent, "A cancelled action was sent.");
	}

	private static UiInputRequest Click()
		=> new(
			Guid.NewGuid(),
			UiTarget.FromId(_id),
			UiControlPart.Instance,
			new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.None),
			null,
			TimeSpan.FromSeconds(5));

	private static UiInputDispatcher Create(CountingResolver resolver, DrawingDriver driver)
	{
		var nodes = new UiNodeRegistry(Guid.NewGuid());
		var revisions = new UiRevisionTracker();

		nodes.Register(new UiSubject(_id, new TestControl()), "button", true);
		resolver.Nodes = nodes;
		resolver.Revisions = revisions;

		return new UiInputDispatcher(
			nodes,
			new UiAdapterRegistry(),
			revisions,
			resolver,
			driver,
			new ImmediateExecutor(),
			new UiActionJournal());
	}

	private sealed class CountingResolver : IUiInputTargetResolver
	{
		public UiNodeRegistry Nodes { get; set; }

		public UiRevisionTracker Revisions { get; set; }

		public int Resolved { get; private set; }

		public UiResolvedInputTarget Resolve(UiSubject subject, UiTargetPart part, UiInputAction action, UiCaptureContext context)
		{
			Resolved++;

			return new(Nodes.GetReference(subject.Id), "window:test", new UiRect(0, 0, 10, 10), new UiPoint(5, 5), Revisions.Read(subject.Id));
		}
	}

	// A window that has drawn what it laid out once the case says so.
	private sealed class DrawingDriver(Task drawn) : IUiInputDriver
	{
		public UiTarget WaitedFor { get; private set; }

		public int Sent { get; private set; }

		public string BackendId => "test";

		public ImmutableArray<string> Capabilities { get; } = ["input.click"];

		public UiError Refusal => null;

		public Task DrawnAsync(UiTarget target, CancellationToken cancellationToken)
		{
			WaitedFor = target;

			return drawn.WaitAsync(cancellationToken);
		}

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
