namespace StockSharp.DesktopDriver.Runtime;

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Api;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Serialization;
using StockSharp.DesktopDriver.Session;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Waiting;

/// <summary>
/// What a caller inside the process talks to.
/// </summary>
/// <remarks>
/// The same interface the client across the channel implements, and the only implementation that does
/// any work: the client forwards, the command line and the agent tools call the client. One operation
/// exists once, so the answer cannot depend on which door the caller came through.
/// </remarks>
public sealed class UiAutomationService(
	UiSessionInfo session,
	IUiNodeRegistry registry,
	IUiAdapterRegistry adapters,
	IUiRootSource roots,
	UiSnapshotService snapshots,
	IUiRevisionTracker revisions,
	UiSemanticTreeBuilder trees,
	UiWaitService waits,
	UiInputDispatcher input,
	UiActionJournal journal,
	UiDiagnosticBuffer diagnostics,
	UiArtifactStore artifacts,
	IUiExecutor executor) : IUiAutomationApi
{
	private readonly UiSessionInfo _session = session ?? throw new ArgumentNullException(nameof(session));
	private readonly IUiNodeRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
	private readonly IUiAdapterRegistry _adapters = adapters ?? throw new ArgumentNullException(nameof(adapters));
	private readonly IUiRootSource _roots = roots ?? throw new ArgumentNullException(nameof(roots));
	private readonly UiSnapshotService _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
	private readonly IUiRevisionTracker _revisions = revisions ?? throw new ArgumentNullException(nameof(revisions));
	private readonly UiSemanticTreeBuilder _trees = trees ?? throw new ArgumentNullException(nameof(trees));
	private readonly UiWaitService _waits = waits ?? throw new ArgumentNullException(nameof(waits));
	private readonly UiInputDispatcher _input = input ?? throw new ArgumentNullException(nameof(input));
	private readonly UiActionJournal _journal = journal ?? throw new ArgumentNullException(nameof(journal));
	private readonly UiDiagnosticBuffer _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
	private readonly UiArtifactStore _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
	private readonly IUiExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

	// A control that keeps moving cannot be read consistently, and answering two mixed states would be
	// worse than refusing. The same bound the snapshot path uses.
	private const int _maxReadRetries = 2;

	/// <summary>
	/// Takes pictures, when the host provided something that can.
	/// </summary>
	public Func<UiScreenshotRequest, CancellationToken, Task<UiScreenshotInfo>> Screenshots { get; set; }

	/// <inheritdoc />
	public Task<UiSessionInfo> GetSessionAsync(CancellationToken cancellationToken)
		=> Task.FromResult(_session);

	/// <inheritdoc />
	public Task<ImmutableArray<UiSurfaceInfo>> GetSurfacesAsync(CancellationToken cancellationToken)
		=> _executor.InvokeAsync(_roots.GetSurfaces, cancellationToken);

	/// <inheritdoc />
	public Task<UiFindResult> FindAsync(UiFindQuery query, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);

		if (query.Selector is null || query.Selector.IsEmpty)
			throw UiErrors.Invalid("A search with no criteria would match the whole application.");

		var page = query.Page ?? UiPageRequest.Default;

		return _executor.InvokeAsync(() => Find(query.Selector, page), cancellationToken);
	}

	/// <inheritdoc />
	public Task<UiTreeSnapshot> GetTreeAsync(UiTreeQuery query, CancellationToken cancellationToken)
		=> _trees.BuildAsync(query, cancellationToken);

	/// <inheritdoc />
	public Task<UiNodeSnapshot> CaptureAsync(
		UiTarget target,
		UiCaptureOptions options,
		CancellationToken cancellationToken)
		=> _snapshots.CaptureAsync(target, options, cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<GridColumnSnapshot>> ReadGridColumnsAsync(
		UiTarget target,
		GridColumnsQuery query,
		CancellationToken cancellationToken)
		=> ReadAsync<IUiGridAdapter, UiDataPage<GridColumnSnapshot>>(
			target,
			"grid.columns",
			query?.Guard,
			(adapter, subject, context) => adapter.ReadColumns(subject, query, context),
			cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<GridRowSnapshot>> ReadGridRowsAsync(
		UiTarget target,
		GridRowsQuery query,
		CancellationToken cancellationToken)
		=> ReadAsync<IUiGridAdapter, UiDataPage<GridRowSnapshot>>(
			target,
			"grid.rows",
			query?.Guard,
			(adapter, subject, context) => adapter.ReadRows(subject, query, context),
			cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<GridGroupSnapshot>> ReadGridGroupsAsync(
		UiTarget target,
		GridGroupsQuery query,
		CancellationToken cancellationToken)
		=> ReadAsync<IUiGridAdapter, UiDataPage<GridGroupSnapshot>>(
			target,
			"grid.groups",
			query?.Guard,
			(adapter, subject, context) => adapter.ReadGroups(subject, query, context),
			cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<ChartSeriesSnapshot>> ReadChartSeriesAsync(
		UiTarget target,
		ChartSeriesQuery query,
		CancellationToken cancellationToken)
		=> ReadAsync<IUiChartAdapter, UiDataPage<ChartSeriesSnapshot>>(
			target,
			"chart.series",
			query?.Guard,
			(adapter, subject, context) => adapter.ReadSeries(subject, query, context),
			cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<ChartPointSnapshot>> ReadChartPointsAsync(
		UiTarget target,
		ChartPointsQuery query,
		CancellationToken cancellationToken)
		=> ReadAsync<IUiChartAdapter, UiDataPage<ChartPointSnapshot>>(
			target,
			"chart.points",
			query?.Guard,
			(adapter, subject, context) => adapter.ReadPoints(subject, query, context),
			cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<OrderBookLevelSnapshot>> ReadOrderBookLevelsAsync(
		UiTarget target,
		OrderBookLevelsQuery query,
		CancellationToken cancellationToken)
		=> ReadAsync<IUiOrderBookAdapter, UiDataPage<OrderBookLevelSnapshot>>(
			target,
			"orderBook.levels",
			query?.Guard,
			(adapter, subject, context) => adapter.ReadLevels(subject, query, context),
			cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<PropertyItemSnapshot>> ReadPropertyEditorItemsAsync(
		UiTarget target,
		PropertyEditorItemsQuery query,
		CancellationToken cancellationToken)
		=> ReadAsync<IUiPropertyEditorAdapter, UiDataPage<PropertyItemSnapshot>>(
			target,
			"propertyEditor.items",
			query?.Guard,
			(adapter, subject, context) => adapter.ReadItems(subject, query, context),
			cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<TreeItemSnapshot>> ReadTreeItemsAsync(
		UiTarget target,
		TreeItemsQuery query,
		CancellationToken cancellationToken)
		=> ReadAsync<IUiTreeAdapter, UiDataPage<TreeItemSnapshot>>(
			target,
			"tree.items",
			query?.Guard,
			(adapter, subject, context) => adapter.ReadItems(subject, query, context),
			cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<DocumentLineSnapshot>> ReadDocumentContentAsync(
		UiTarget target,
		DocumentContentQuery query,
		CancellationToken cancellationToken)
		=> ReadAsync<IUiDocumentAdapter, UiDataPage<DocumentLineSnapshot>>(
			target,
			"document.content",
			query?.Guard,
			(adapter, subject, context) => adapter.ReadContent(subject, query, context),
			cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<DiagramNodeSnapshot>> ReadDiagramNodesAsync(
		UiTarget target,
		DiagramNodesQuery query,
		CancellationToken cancellationToken)
		=> ReadAsync<IUiDiagramAdapter, UiDataPage<DiagramNodeSnapshot>>(
			target,
			"diagram.nodes",
			query?.Guard,
			(adapter, subject, context) => adapter.ReadNodes(subject, query, context),
			cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<DiagramConnectionSnapshot>> ReadDiagramConnectionsAsync(
		UiTarget target,
		DiagramConnectionsQuery query,
		CancellationToken cancellationToken)
		=> ReadAsync<IUiDiagramAdapter, UiDataPage<DiagramConnectionSnapshot>>(
			target,
			"diagram.connections",
			query?.Guard,
			(adapter, subject, context) => adapter.ReadConnections(subject, query, context),
			cancellationToken);

	/// <inheritdoc />
	public Task<DockLayoutSnapshot> ReadDockLayoutAsync(
		UiTarget target,
		DockLayoutQuery query,
		CancellationToken cancellationToken)
		=> ReadAsync<IUiDockAdapter, DockLayoutSnapshot>(
			target,
			"dock.layout",
			query?.Guard,
			(adapter, subject, context) => adapter.ReadLayout(subject, query, context),
			cancellationToken);

	/// <inheritdoc />
	public Task<UiActionReceipt> ExecuteInputAsync(
		UiInputRequest request,
		CancellationToken cancellationToken)
		=> _input.ExecuteAsync(request, cancellationToken);

	/// <inheritdoc />
	public Task<UiActionReceipt> GetActionStatusAsync(Guid actionId, CancellationToken cancellationToken)
	{
		var receipt = _journal.Find(actionId)
			?? throw UiErrors.NotFound($"This session has no action {actionId}.");

		return Task.FromResult(receipt);
	}

	/// <inheritdoc />
	public Task<UiWaitResult> WaitAsync(UiWaitRequest request, CancellationToken cancellationToken)
		=> _waits.WaitAsync(request, cancellationToken);

	/// <inheritdoc />
	public Task<UiScreenshotInfo> CaptureScreenshotAsync(
		UiScreenshotRequest request,
		CancellationToken cancellationToken)
	{
		if (Screenshots is null)
			throw UiErrors.Unsupported("This host cannot take pictures.");

		return Screenshots(request, cancellationToken);
	}

	/// <inheritdoc />
	public Task<UiArtifactChunk> ReadArtifactAsync(
		UiArtifactReadRequest request,
		CancellationToken cancellationToken)
		=> Task.FromResult(_artifacts.Read(request));

	/// <inheritdoc />
	public Task<UiDiagnosticPage> ReadDiagnosticsAsync(
		UiDiagnosticQuery query,
		CancellationToken cancellationToken)
		=> Task.FromResult(_diagnostics.Read(query));

	// A search walks as far as an address is resolved, and no less. A search that gave up earlier than the
	// registry does would answer that a control is not there while naming it directly still reaches it, and
	// the two answers would disagree about the same window. Reached on a real product: the shell's docked
	// panels sit past two thousand nodes, and every search for one of them came back empty.
	private static readonly UiReadBudget _searchBudget = new(MaxDepth: 64, MaxNodes: 20000, MaxItems: 2000);

	private UiFindResult Find(UiSelector selector, UiPageRequest page)
	{
		var found = ImmutableArray.CreateBuilder<UiNodeRef>();
		var tree = _trees.Build(new UiTreeQuery(null, false, UiCaptureOptions.Default with { Budget = _searchBudget }));

		foreach (var node in tree.Nodes)
		{
			if (!Matches(node, selector))
				continue;

			if (found.Count >= page.Limit)
				return new UiFindResult(found.ToImmutable(), true, null);

			found.Add(node.Node);
		}

		// The walk itself stopping short is not the same answer as nothing matching, and a caller that could
		// not tell them apart would read "no such control" off a search that never looked.
		return new UiFindResult(found.ToImmutable(), tree.Truncated, null);
	}

	private static bool Matches(UiNodeSnapshot node, UiSelector selector)
	{
		if (selector.ScopeId is { Length: > 0 } scope && node.Node.Id.ScopeId != scope)
			return false;

		if (selector.AutomationId is { Length: > 0 } automationId && node.Node.Id.LocalId != automationId)
			return false;

		if (selector.Name is { Length: > 0 } name && node.Node.Id.LocalId != name)
			return false;

		if (selector.Kind is { Length: > 0 } kind && node.Node.Kind != kind)
			return false;

		if (selector.SurfaceId is { Length: > 0 } surface &&
			(node.Presentation.SurfaceId is not Values.UiKnown<string> known || known.Value != surface))
		{
			return false;
		}

		if (selector.Text is { Length: > 0 } text)
		{
			if (node.State is not BasicState basic ||
				basic.Text is not Values.UiKnown<string> actual ||
				!string.Equals(actual.Value, text, StringComparison.Ordinal))
			{
				return false;
			}
		}

		return true;
	}

	// Every paged read goes through here, so the guard is enforced in one place rather than in twelve
	// adapters that would each have to remember to do it.
	private Task<TResult> ReadAsync<TAdapter, TResult>(
		UiTarget target,
		string capability,
		UiReadGuard guard,
		Func<TAdapter, UiSubject, UiCaptureContext, TResult> read,
		CancellationToken cancellationToken)
		where TAdapter : class
	{
		ArgumentNullException.ThrowIfNull(target);

		return _executor.InvokeAsync(
			() =>
			{
				var subject = _registry.Resolve(target);

				if (_adapters.Resolve(subject) is not TAdapter adapter)
				{
					throw UiErrors.Unsupported(
						$"{subject.Id} cannot answer {capability}: nothing registered for it reads that.");
				}

				var reference = _registry.GetReference(subject.Id);
				var strict = UiReadGuards.IsStrict(guard);

				for (var attempt = 0; ; attempt++)
				{
					var before = _revisions.Read(subject.Id);

					if (strict)
						UiReadGuards.Enforce(guard, before);

					var context = new UiCaptureContext(
						reference,
						before,
						UiCaptureOptions.Default with { Guard = guard });

					var page = read(adapter, subject, context);
					var after = _revisions.Read(subject.Id);

					if (!strict || Equals(before, after))
						return Stamped(page, after, strict);

					if (attempt >= _maxReadRetries)
					{
						throw UiErrors.Changed(
							$"{subject.Id} kept changing while {capability} was being read; the answer would be " +
							"two states mixed together.");
					}
				}
			},
			cancellationToken);
	}

	// An adapter reads one control and has neither the running copy's identity nor the revisions either
	// side of its read, so the answer is stamped here, where both are in hand.
	private TResult Stamped<TResult>(TResult page, UiRevisions revisions, bool strict)
		=> page is IUiStamped stamped
			? (TResult)stamped.WithStamp(UiReadGuards.Stamp(_session.InstanceId, revisions, strict))
			: page;
}
