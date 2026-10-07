namespace StockSharp.DesktopDriver.Runtime;

using System;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Turns a request to do something into input that was actually sent.
/// </summary>
/// <remarks>
/// Actions are performed one at a time. Two clicks in flight at once on the same desktop would arrive in
/// whatever order the input system chose, and neither test would know which one it had observed.
/// </remarks>
public sealed class UiInputDispatcher(
	IUiNodeRegistry registry,
	IUiAdapterRegistry adapters,
	IUiRevisionTracker revisions,
	IUiInputTargetResolver resolver,
	IUiInputDriver driver,
	IUiExecutor executor,
	UiActionJournal journal)
{
	private readonly SemaphoreSlim _oneAtATime = new(1, 1);
	private readonly IUiNodeRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
	private readonly IUiAdapterRegistry _adapters = adapters ?? throw new ArgumentNullException(nameof(adapters));
	private readonly IUiRevisionTracker _revisions = revisions ?? throw new ArgumentNullException(nameof(revisions));
	private readonly IUiInputTargetResolver _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
	private readonly IUiInputDriver _driver = driver ?? throw new ArgumentNullException(nameof(driver));
	private readonly IUiExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));
	private readonly UiActionJournal _journal = journal ?? throw new ArgumentNullException(nameof(journal));

	/// <summary>
	/// Does something to the interface.
	/// </summary>
	/// <param name="request">What to do.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>What became of it.</returns>
	public async Task<UiActionReceipt> ExecuteAsync(UiInputRequest request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (!_journal.TryClaim(request, out var already))
			return already ?? Refused(request, UiActionStatuses.OutcomeUnknown, "That action is still running.");

		await _oneAtATime.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			// Asked again immediately before anything is sent: an action cancelled while it waited its
			// turn must not go out afterwards just because it reached the front of the queue.
			if (cancellationToken.IsCancellationRequested)
			{
				var cancelled = Refused(request, UiActionStatuses.CancelledBeforeDispatch, "Cancelled before anything was sent.");
				_journal.Complete(cancelled);

				return cancelled;
			}

			// Before the control is resolved, let alone offered the action: a control that performs its own
			// input is not a way round a run that refuses it.
			if (request.Action is not UiEnsureVisibleAction && _driver.Refusal is { } refusal)
				throw new UiAutomationException(refusal);

			var resolved = await _executor.InvokeAsync(() => Resolve(request), cancellationToken).ConfigureAwait(false);

			// Bringing a part onto the screen is the resolving step and nothing else: the adapter has
			// already moved the view by the time the target comes back, and no input is sent for it.
			var receipt = request.Action is UiEnsureVisibleAction
				? Shown(request)
				: await PerformedAsync(request, cancellationToken).ConfigureAwait(false)
					?? await _driver.ExecuteAsync(request, resolved, cancellationToken).ConfigureAwait(false);

			receipt = receipt with
			{
				Before = UiField<Snapshots.UiRevisions>.Known(resolved.Revisions),
				After = UiField<Snapshots.UiRevisions>.Known(_revisions.Read(resolved.Node.Id)),
			};

			_journal.Complete(receipt);

			return receipt;
		}
		catch (OperationCanceledException)
		{
			var cancelled = Refused(request, UiActionStatuses.CancelledBeforeDispatch, "Cancelled.");
			_journal.Complete(cancelled);

			throw;
		}
		catch (UiAutomationException error)
		{
			var failed = new UiActionReceipt(
				request.ActionId,
				UiActionStatuses.FailedBeforeDispatch,
				_driver.BackendId,
				UiField<Snapshots.UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
				UiField<Snapshots.UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
				false,
				error.Error);

			_journal.Complete(failed);

			throw;
		}
		finally
		{
			_oneAtATime.Release();
		}
	}

	// After the target has been resolved, so that a part which is missing, covered or disabled has already
	// been refused, and only then offered to the control's own family. Null when nobody takes it, which is
	// the ordinary case and leaves the input backend to send the action as events.
	private async Task<UiActionReceipt> PerformedAsync(UiInputRequest request, CancellationToken cancellationToken)
	{
		var subject = _registry.Resolve(request.Target);

		if (_adapters.Resolve(subject) is not IUiInputPerformingAdapter adapter)
			return null;

		var taken = await _executor
			.InvokeAsync(
				() =>
				{
					if (!adapter.CanPerform(subject, request.Part, request.Action))
						return false;

					adapter.Perform(subject, request.Part, request.Action);

					return true;
				},
				cancellationToken)
			.ConfigureAwait(false);

		return taken ? Shown(request) with { AnyInputDispatched = true } : null;
	}

	private UiResolvedInputTarget Resolve(UiInputRequest request)
	{
		var subject = _registry.Resolve(request.Target);
		var reference = _registry.GetReference(subject.Id);
		var current = _revisions.Read(subject.Id);

		if (request.Guard is { } guard && !guard.IsEmpty)
		{
			if (guard.ExpectedState is not null && !Equals(guard.ExpectedState, current.State))
				throw UiErrors.Changed("The node changed between the caller reading it and acting on it.");

			if (guard.ExpectedView is not null && !Equals(guard.ExpectedView, current.View))
				throw UiErrors.Changed("What the node shows changed between the caller reading it and acting on it.");

			if (guard.ExpectedLayout is not null && !Equals(guard.ExpectedLayout, current.Layout))
				throw UiErrors.Changed("The node moved between the caller reading it and acting on it.");
		}

		var context = new UiCaptureContext(reference, current, UiCaptureOptions.Default);

		if (_adapters.Resolve(subject) is IUiInputTargetAdapter adapter &&
			adapter.SupportsTargetPart(subject, request.Part))
		{
			return adapter.ResolveInputTarget(subject, request.Part, request.Action, context);
		}

		return _resolver.Resolve(subject, request.Part, request.Action, context);
	}

	// The revisions are filled in by the caller of this, the same way a driver's receipt is.
	private UiActionReceipt Shown(UiInputRequest request)
		=> new(
			request.ActionId,
			UiActionStatuses.Dispatched,
			_driver.BackendId,
			UiField<Snapshots.UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
			UiField<Snapshots.UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
			false,
			null);

	private UiActionReceipt Refused(UiInputRequest request, UiActionStatuses status, string message)
		=> new(
			request.ActionId,
			status,
			_driver.BackendId,
			UiField<Snapshots.UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
			UiField<Snapshots.UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
			false,
			UiError.Create(UiErrorCodes.Cancelled, message));
}
