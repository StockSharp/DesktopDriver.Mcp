namespace StockSharp.DesktopDriver.Runtime;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Walks the interface and returns it as nodes and the edges between them.
/// </summary>
/// <remarks>
/// Every node appears once. A document shown in a tab and listed in a tree is one node with two edges
/// pointing at it, not two copies that a reader would have to notice are the same.
/// <para>
/// The walk is bounded in depth, in node count and in how many children any one container contributes,
/// and it says when a bound stopped it. An interface is a graph, not a tree: a cycle is cut with a
/// warning rather than followed until something runs out.
/// </para>
/// </remarks>
public sealed class UiSemanticTreeBuilder(
	Guid instanceId,
	UiSnapshotService snapshots,
	IUiNodeRegistry registry,
	IUiAdapterRegistry adapters,
	IUiRootSource roots,
	IUiExecutor executor) : IUiNodeLocator
{
	// Wide enough for a real application's window, and still an end: a search that never stopped would
	// hang the interface it is searching rather than report that the control is not there.
	private static readonly UiReadBudget _searchBudget = new(MaxDepth: 64, MaxNodes: 20000, MaxItems: 2000);

	private readonly Guid _instanceId = instanceId;
	private readonly UiSnapshotService _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
	private readonly IUiNodeRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
	private readonly IUiAdapterRegistry _adapters = adapters ?? throw new ArgumentNullException(nameof(adapters));
	private readonly IUiRootSource _roots = roots ?? throw new ArgumentNullException(nameof(roots));
	private readonly IUiExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

	/// <summary>
	/// Walks the tree.
	/// </summary>
	/// <param name="query">Where to start and how far to go.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The nodes and the edges.</returns>
	public Task<UiTreeSnapshot> BuildAsync(UiTreeQuery query, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);

		return _executor.InvokeAsync(() => Build(query), cancellationToken);
	}

	/// <summary>
	/// Walks the tree, on the thread the interface belongs to.
	/// </summary>
	/// <param name="query">Where to start and how far to go.</param>
	/// <returns>The nodes and the edges.</returns>
	public UiTreeSnapshot Build(UiTreeQuery query)
	{
		ArgumentNullException.ThrowIfNull(query);

		var options = query.Options ?? UiCaptureOptions.Default;
		var budget = options.Budget ?? UiReadBudget.Default;
		var nodes = ImmutableArray.CreateBuilder<UiNodeSnapshot>();
		var links = ImmutableArray.CreateBuilder<UiChildLink>();
		var warnings = ImmutableArray.CreateBuilder<string>();
		var seen = new HashSet<UiNodeId>();
		var queue = new Queue<(UiSubject Subject, int Depth)>();
		var truncated = false;

		foreach (var root in StartingPoints(query, budget))
			queue.Enqueue((root, 0));

		while (queue.Count > 0)
		{
			var (subject, depth) = queue.Dequeue();

			if (!seen.Add(subject.Id))
			{
				// Reached a second time: the edge was already recorded, the node is already in the reply.
				continue;
			}

			if (nodes.Count >= budget.MaxNodes)
			{
				truncated = true;
				warnings.Add($"The walk stopped at {budget.MaxNodes} nodes.");
				break;
			}

			nodes.Add(_snapshots.Capture(UiTarget.FromId(subject.Id), options));

			if (depth >= budget.MaxDepth)
			{
				truncated = true;
				continue;
			}

			if (_adapters.Resolve(subject) is not IUiContainerAdapter container)
				continue;

			var reference = _registry.GetReference(subject.Id);
			var context = new UiCaptureContext(reference, new UiRevisions(null, null, null), options);
			var page = container.ReadChildren(subject, new UiPageRequest(budget.MaxItems, null), context);

			if (page.Truncated)
			{
				truncated = true;
				warnings.Add($"{subject.Id} has more children than the budget allows.");
			}

			foreach (var link in page.Items)
			{
				links.Add(link);

				if (link.Relation == UiRelations.OwnedReference)
				{
					// Owned references point at nodes that belong somewhere else. Following them by default
					// would drag the whole application into every reply.
					continue;
				}

				if (seen.Contains(link.Child.Id))
					continue;

				if (!_registry.TryResolve(link.Child.Id, out var child))
				{
					warnings.Add($"{link.Child.Id} is linked from {subject.Id} but is not registered.");
					continue;
				}

				queue.Enqueue((child, depth + 1));
			}
		}

		return new UiTreeSnapshot(
			_instanceId,
			Guid.NewGuid(),
			nodes.ToImmutable(),
			links.ToImmutable(),
			truncated,
			warnings.ToImmutable());
	}

	/// <inheritdoc />
	public UiSubject Locate(UiNodeId id)
	{
		ArgumentNullException.ThrowIfNull(id);

		var seen = new HashSet<UiNodeId>();
		var queue = new Queue<(UiSubject Subject, int Depth)>();
		var visited = 0;

		foreach (var root in _roots.GetRoots(_searchBudget))
			queue.Enqueue((root, 0));

		while (queue.Count > 0)
		{
			var (subject, depth) = queue.Dequeue();

			if (!seen.Add(subject.Id) || ++visited > _searchBudget.MaxNodes)
				continue;

			if (subject.Id == id)
				return subject;

			if (depth >= _searchBudget.MaxDepth || _adapters.Resolve(subject) is not IUiContainerAdapter container)
				continue;

			var reference = _registry.GetReference(subject.Id);
			var context = new UiCaptureContext(reference, new UiRevisions(null, null, null), UiCaptureOptions.Default);

			// Reading a container's children is what gives them addresses, so the search finds the node it
			// is looking for by the same act that makes the node addressable at all.
			foreach (var link in container.ReadChildren(subject, new UiPageRequest(_searchBudget.MaxItems, null), context).Items)
			{
				if (link.Relation == UiRelations.OwnedReference || seen.Contains(link.Child.Id))
					continue;

				if (_registry.TryResolve(link.Child.Id, out var child))
					queue.Enqueue((child, depth + 1));
			}
		}

		return null;
	}

	private IEnumerable<UiSubject> StartingPoints(UiTreeQuery query, UiReadBudget budget)
	{
		if (query.Root is not null)
			return [_registry.Resolve(query.Root)];

		return _roots.GetRoots(budget);
	}
}
