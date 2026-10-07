namespace StockSharp.DesktopDriver.Runtime;

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Reads one node: finds it, asks its adapter, and wraps the answer in the envelope every reply carries.
/// </summary>
/// <remarks>
/// The adapter answers only for the control. Identity, geometry, versions and the stamp are added here,
/// once, so that thirty adapters cannot disagree about what a snapshot looks like.
/// </remarks>
public sealed class UiSnapshotService(
	Guid instanceId,
	IUiNodeRegistry registry,
	IUiAdapterRegistry adapters,
	IUiRevisionTracker revisions,
	IUiPresentationReader presentation,
	IUiExecutor executor)
{
	private const int _maxRetries = 3;

	private readonly Guid _instanceId = instanceId;
	private readonly IUiNodeRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
	private readonly IUiAdapterRegistry _adapters = adapters ?? throw new ArgumentNullException(nameof(adapters));
	private readonly IUiRevisionTracker _revisions = revisions ?? throw new ArgumentNullException(nameof(revisions));
	private readonly IUiPresentationReader _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
	private readonly IUiExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

	/// <summary>
	/// Reads a node.
	/// </summary>
	/// <param name="target">Which node.</param>
	/// <param name="options">How much of it.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The node.</returns>
	public Task<UiNodeSnapshot> CaptureAsync(
		UiTarget target,
		UiCaptureOptions options,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(target);

		options ??= UiCaptureOptions.Default;

		return _executor.InvokeAsync(() => Capture(target, options), cancellationToken);
	}

	/// <summary>
	/// Reads a node, on the thread the interface belongs to.
	/// </summary>
	/// <param name="target">Which node.</param>
	/// <param name="options">How much of it.</param>
	/// <returns>The node.</returns>
	public UiNodeSnapshot Capture(UiTarget target, UiCaptureOptions options)
	{
		ArgumentNullException.ThrowIfNull(target);

		options ??= UiCaptureOptions.Default;

		var subject = _registry.Resolve(target);
		var reference = _registry.GetReference(subject.Id)
			?? throw UiErrors.NotFound($"Nothing is registered as {subject.Id}.");

		var guard = options.Guard;
		var strict = UiReadGuards.IsStrict(guard);

		for (var attempt = 0; ; attempt++)
		{
			var before = _revisions.Read(subject.Id);

			if (strict)
				UiReadGuards.Enforce(guard, before);

			var context = new UiCaptureContext(reference, before, options);
			var adapter = _adapters.Resolve(subject);
			var capture = Read(adapter, subject, context);
			var view = _presentation.Read(subject, context);
			var after = _revisions.Read(subject.Id);

			if (!strict || Equals(before, after))
			{
				return new UiNodeSnapshot(
					reference,
					new UiSnapshotStamp(
						Serialization.UiJson.SchemaVersion,
						_instanceId,
						Guid.NewGuid(),
						DateTime.UtcNow,
						after,
						strict ? UiConsistencies.VersionChecked : UiConsistencies.UiThreadRead),
					view,
					capture.Ready,
					adapter?.GetCapabilities(subject) ?? ImmutableArray<string>.Empty,
					capture.State,
					capture.Completeness,
					capture.Warnings);
			}

			if (attempt >= _maxRetries)
			{
				throw UiErrors.Changed(
					$"{subject.Id} kept changing while it was being read; the answer would be two states mixed together.");
			}
		}
	}

	// A control nobody wrote an adapter for is still a node: it has a place, a size and a name, and
	// saying so is more useful than refusing to answer at all.
	private static UiStateCapture Read(IUiSnapshotAdapter adapter, UiSubject subject, UiCaptureContext context)
	{
		if (adapter is not null)
			return adapter.CaptureState(subject, context);

		if (subject.Instance is IUiSnapshotProvider provider)
			return provider.CaptureState(context);

		return new UiStateCapture(
			new BasicState(
				UiField<string>.Unavailable(UiUnavailableReasons.Unsupported, "no adapter"),
				UiField<UiValue>.Unavailable(UiUnavailableReasons.Unsupported, "no adapter"),
				UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "no adapter"),
				UiField<bool?>.Unavailable(UiUnavailableReasons.Unsupported, "no adapter"),
				UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "no adapter"),
				UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "no adapter"),
				UiField<UiNodeId>.Unavailable(UiUnavailableReasons.Unsupported, "no adapter"),
				UiField<UiNodeId>.Unavailable(UiUnavailableReasons.Unsupported, "no adapter"),
				ImmutableArray<string>.Empty),
			UiReadyStatuses.Unknown,
			new UiCompleteness(true, ["state"]),
			[$"No adapter is registered for {subject.Instance.GetType().Name}."]);
	}
}
