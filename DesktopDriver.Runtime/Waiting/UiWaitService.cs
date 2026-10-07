namespace StockSharp.DesktopDriver.Runtime;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Waiting;

/// <summary>
/// Waits until a node satisfies a condition, or until the time runs out.
/// </summary>
/// <remarks>
/// Woken by the node's own revisions when there are any, and by a short poll otherwise, because a
/// control that changes without telling anyone still changes. Neither path holds the interface's thread:
/// each look is one short read, and the waiting happens between them.
/// <para>
/// Time is measured monotonically. A wait measured against the wall clock ends early or late whenever the
/// machine's clock is corrected, which on a test machine is exactly when a long suite is running.
/// </para>
/// </remarks>
public sealed class UiWaitService(UiSnapshotService snapshots, IUiRevisionTracker revisions)
{
	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan _maximumTimeout = TimeSpan.FromSeconds(120);
	private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(50);

	private readonly UiSnapshotService _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
	private readonly IUiRevisionTracker _revisions = revisions ?? throw new ArgumentNullException(nameof(revisions));

	/// <summary>
	/// Waits for a condition.
	/// </summary>
	/// <param name="request">What to wait for and for how long.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>What was observed when the condition held.</returns>
	public async Task<UiWaitResult> WaitAsync(UiWaitRequest request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		var timeout = request.Timeout <= TimeSpan.Zero ? _defaultTimeout : request.Timeout;

		if (timeout > _maximumTimeout)
			throw UiErrors.Invalid($"A wait may not be longer than {_maximumTimeout.TotalSeconds:F0} seconds.");

		var options = request.CaptureOptions ?? UiCaptureOptions.Default;
		var watch = Stopwatch.StartNew();
		var observations = 0;
		UiNodeSnapshot last = null;
		IDisposable subscription = null;
		var changed = new SemaphoreSlim(0, 1);

		try
		{
			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();

				observations++;
				last = await LookAsync(request.Target, options, cancellationToken).ConfigureAwait(false);

				if (UiConditionEvaluator.Evaluate(request.Condition, last) == UiConditionResults.True)
					return new UiWaitResult(watch.Elapsed, observations, last);

				subscription ??= Watch(last, changed);

				var left = timeout - watch.Elapsed;

				if (left <= TimeSpan.Zero)
				{
					throw new UiAutomationException(new UiError(
						UiErrorCodes.Timeout,
						$"The condition did not hold within {timeout.TotalSeconds:F1} seconds; {observations} looks were taken.",
						null,
						null,
						System.Collections.Immutable.ImmutableDictionary<string, string>.Empty
							.Add("elapsedMs", ((long)watch.Elapsed.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture))
							.Add("observations", observations.ToString(System.Globalization.CultureInfo.InvariantCulture))
							.Add("lastReady", last?.Ready.ToString() ?? "absent")));
				}

				await changed.WaitAsync(Min(left, _pollInterval), cancellationToken).ConfigureAwait(false);
			}
		}
		finally
		{
			subscription?.Dispose();
			changed.Dispose();
		}
	}

	private IDisposable Watch(UiNodeSnapshot snapshot, SemaphoreSlim changed)
	{
		if (snapshot?.Node?.Id is not UiNodeId id)
			return null;

		return _revisions.Subscribe(id, _ =>
		{
			if (changed.CurrentCount == 0)
			{
				try
				{
					changed.Release();
				}
				catch (SemaphoreFullException)
				{
					// Another change arrived first; one wake-up is enough to look again.
				}
				catch (ObjectDisposedException)
				{
					// The wait is over.
				}
			}
		});
	}

	// A node that is not there is an observation, not a failure: waiting for something to appear - or to
	// disappear - is the point of the condition.
	private async Task<UiNodeSnapshot> LookAsync(
		UiTarget target,
		UiCaptureOptions options,
		CancellationToken cancellationToken)
	{
		try
		{
			return await _snapshots.CaptureAsync(target, options, cancellationToken).ConfigureAwait(false);
		}
		catch (UiAutomationException error) when (error.Error.Code is UiErrorCodes.NotFound or UiErrorCodes.NotCreated)
		{
			return null;
		}
	}

	private static TimeSpan Min(TimeSpan first, TimeSpan second) => first < second ? first : second;
}
