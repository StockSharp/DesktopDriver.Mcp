namespace StockSharp.DesktopDriver.Avalonia.Docking;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using global::Avalonia.Controls;

using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Avalonia;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Reads a docking workspace: its groups, its panels, and where each panel's content is.
/// </summary>
/// <remarks>
/// It says where the content is and never what the content holds. That boundary is the whole point: the
/// same table is read the same way whether it is docked, floating or in an ordinary window, and a table
/// never has to know which of those it is in.
/// <para>
/// Reading a layout does not build anything. A panel that has never been shown is reported as having no
/// content yet, because a reader that created it to describe it would change the thing it was asked
/// about - and a lazily built panel is usually exactly what the test is about.
/// </para>
/// </remarks>
public sealed class DockAdapter(AvaloniaNodeBinder binder)
	: AvaloniaControlAdapter(binder), IUiDockAdapter, IUiInputTargetAdapter
{
	private readonly AvaloniaNodeBinder _binder = binder ?? throw new ArgumentNullException(nameof(binder));

	/// <inheritdoc />
	public override string Kind => "workspace";

	/// <inheritdoc />
	public override bool CanHandle(UiSubject subject) => subject?.Instance is DockControl;

	/// <inheritdoc />
	public bool SupportsTargetPart(UiSubject subject, UiTargetPart part)
		=> subject?.Instance is DockControl && part is UiDockTabPart or UiDockClosePart;

	/// <inheritdoc />
	public UiResolvedInputTarget ResolveInputTarget(
		UiSubject subject,
		UiTargetPart part,
		UiInputAction action,
		UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var host = (DockControl)subject.Instance;
		var layout = host.Layout;
		var panelId = part switch
		{
			UiDockTabPart tab => tab.PanelId,
			UiDockClosePart close => close.PanelId,
			_ => throw UiErrors.Unsupported($"A workspace has no part called {part?.Kind}."),
		};

		if (action is UiEnsureVisibleAction)
			DockParts.Show(host, layout, panelId);

		var visual = part is UiDockClosePart
			? DockParts.Close(host, layout, panelId)
			: DockParts.Tab(host, layout, panelId);

		return AvaloniaInputTargetResolver.Locate(
			visual,
			context?.Node,
			context?.Revisions,
			part is UiDockClosePart ? $"close button of '{panelId}'" : $"tab of '{panelId}'");
	}

	/// <inheritdoc />
	public override ImmutableArray<string> GetCapabilities(UiSubject subject)
	{
		var capabilities = ImmutableArray.CreateBuilder<string>();

		capabilities.Add("input.click");
		capabilities.Add("input.key");
		capabilities.Add("tree.children");

		if (((DockControl)subject.Instance).Layout is not null)
			capabilities.Add("dock.layout");

		return capabilities.ToImmutable();
	}

	/// <inheritdoc />
	public override UiStateCapture CaptureState(UiSubject subject, UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var host = (DockControl)subject.Instance;
		var layout = host.Layout;
		var groups = 0L;
		var panels = 0L;

		foreach (var (dockable, _) in Walk(layout))
		{
			if (dockable is IDock)
				groups++;
			else
				panels++;
		}

		return new UiStateCapture(
			new DockState(
				IdOf(layout) ?? "workspace",
				UiField<long>.Known(groups),
				UiField<long>.Known(panels),
				UiField<string>.Known(IdOf(Focused(layout)))),
			layout is null ? UiReadyStatuses.Loading : UiReadyStatuses.Ready,
			UiCompleteness.Complete,
			[]);
	}

	/// <inheritdoc />
	/// <remarks>
	/// The children of a workspace are the panels' contents, not the docking library's own scaffolding.
	/// A walk that returned the scaffolding would bury the panels under the machinery that arranges them.
	/// </remarks>
	public override UiDataPage<UiChildLink> ReadChildren(
		UiSubject subject,
		UiPageRequest query,
		UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var host = (DockControl)subject.Instance;
		var links = ImmutableArray.CreateBuilder<UiChildLink>();
		var order = 0;
		var window = new UiPageWindow(query, "dock.children", "all", "panels");

		foreach (var (dockable, _) in Walk(host.Layout))
		{
			if (dockable is IDock)
				continue;

			if (DockVisuals.ContentOf(host, dockable) is not { } content)
				continue;

			if (!window.Take())
				continue;

			if (_binder.Bind(content) is { } reference)
				links.Add(new UiChildLink(context?.Node?.Id, reference, UiRelations.Content, order++));
		}

		return new UiDataPage<UiChildLink>(
			context?.Node,
			null,
			links.ToImmutable(),
			UiField<long>.Known(window.Total),
			window.Truncated,
			window.NextCursor,
			window.TruncationReason);
	}

	/// <inheritdoc />
	public DockLayoutSnapshot ReadLayout(UiSubject subject, DockLayoutQuery query, UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var host = (DockControl)subject.Instance;
		var layout = host.Layout
			?? throw UiErrors.Fail(UiErrorCodes.NotCreated, "This workspace has no layout yet.");

		var budget = query?.Budget ?? UiReadBudget.Default;
		var nodes = ImmutableArray.CreateBuilder<DockLayoutNode>();
		var warnings = ImmutableArray.CreateBuilder<string>();
		var truncated = false;

		foreach (var (dockable, parent) in Walk(Root(layout, query?.RootLayoutId)))
		{
			if (nodes.Count >= budget.MaxNodes)
			{
				truncated = true;
				warnings.Add($"The layout stopped at {budget.MaxNodes} nodes.");
				break;
			}

			nodes.Add(dockable is IDock dock
				? Group(dock, parent)
				: Panel(host, dockable, parent, layout));
		}

		return new DockLayoutSnapshot(
			context?.Node,
			null,
			[IdOf(layout)],
			nodes.ToImmutable(),
			truncated,
			warnings.ToImmutable());
	}

	private static IDockable Root(IDock layout, string rootLayoutId)
	{
		if (string.IsNullOrEmpty(rootLayoutId))
			return layout;

		foreach (var (dockable, _) in Walk(layout))
		{
			if (string.Equals(IdOf(dockable), rootLayoutId, StringComparison.Ordinal))
				return dockable;
		}

		throw UiErrors.NotFound($"This workspace has no layout node called {rootLayoutId}.");
	}

	private static DockGroupSnapshot Group(IDock dock, IDockable parent)
		=> new(
			IdOf(dock),
			IdOf(parent),
			KindOf(dock),
			dock is IProportionalDock proportional
				? UiField<DockOrientations>.Known(proportional.Orientation == Orientation.Horizontal
					? DockOrientations.Horizontal
					: DockOrientations.Vertical)
				: UiField<DockOrientations>.Unavailable(UiUnavailableReasons.Unsupported, "This group does not divide."),
			[.. Children(dock).Select(IdOf)],
			dock.ActiveDockable is { } active and not IDock
				? UiField<string>.Known(IdOf(active))
				: UiField<string>.Known(null));

	private DockPanelSnapshot Panel(DockControl host, IDockable dockable, IDockable parent, IDock layout)
	{
		var content = DockVisuals.ContentOf(host, dockable);
		var reference = content is null ? null : _binder.Bind(content);
		var selected = parent is IDock group && ReferenceEquals(group.ActiveDockable, dockable);

		return new DockPanelSnapshot(
			IdOf(dockable),
			IdOf(parent),
			reference?.Id,
			dockable.Title,
			selected,
			ReferenceEquals(Focused(layout), dockable),
			dockable is ITool { CanPin: false } or IDocument,
			dockable.Owner is IRootDock { Window: not null },
			content is null
				? UiField<string>.Unavailable(UiUnavailableReasons.NotCreated, "This panel has not been built yet.")
				: UiField<string>.Known(UiAutomationNames.GetSurfaceId(TopLevel.GetTopLevel(content))),
			Presentation(dockable, parent, content, selected),
			content is null ? UiContentStatuses.NotCreated : UiContentStatuses.Created,
			reference is null
				? UiField<UiNodeRef>.Unavailable(UiUnavailableReasons.NotCreated, "This panel has not been built yet.")
				: UiField<UiNodeRef>.Known(reference),
			AvaloniaVisuals.BoundsIn(content, host) is { } bounds
				? UiField<UiRect>.Known(new UiRect(bounds.X, bounds.Y, bounds.Width, bounds.Height))
				: UiField<UiRect>.Unavailable(UiUnavailableReasons.NotCreated, "This panel is not drawn anywhere."));
	}

	// Four states, not one boolean. A panel whose tab exists but is not selected is not a missing panel,
	// and a test that could not tell them apart would pass on a workspace that had lost it.
	private static DockPanelPresentations Presentation(
		IDockable dockable,
		IDockable parent,
		Control content,
		bool selected)
	{
		if (parent is IDock { VisibleDockables: { } visible } && !visible.Contains(dockable))
			return DockPanelPresentations.Hidden;

		if (!selected && parent is IDock)
			return DockPanelPresentations.HiddenTab;

		if (content is null)
			return DockPanelPresentations.Hidden;

		return content.IsEffectivelyVisible ? DockPanelPresentations.Shown : DockPanelPresentations.AutoHidden;
	}

	private static DockGroupKinds KindOf(IDock dock) => dock switch
	{
		IRootDock => DockGroupKinds.Root,
		IProportionalDock => DockGroupKinds.Split,
		IToolDock or IDocumentDock => DockGroupKinds.Tabs,
		_ => DockGroupKinds.Split,
	};

	// A splitter is how a group arranges what is in it, not something in it. Counting one as a panel
	// would make a workspace look as if it had a panel nobody can name, open or read.
	private static IEnumerable<IDockable> Children(IDock dock)
		=> (dock?.VisibleDockables ?? Enumerable.Empty<IDockable>())
			.Where(child => child is not IProportionalDockSplitter and not IGridDockSplitter);

	private static IDockable Focused(IDock layout)
		=> layout is IRootDock root ? root.FocusedDockable : layout?.ActiveDockable;

	private static string IdOf(IDockable dockable) => dockable?.Id;

	// Breadth first, so that a layout read with a budget loses the deepest panels rather than a whole
	// side of the workspace.
	private static IEnumerable<(IDockable Dockable, IDockable Parent)> Walk(IDockable root)
	{
		if (root is null)
			yield break;

		var queue = new Queue<(IDockable Dockable, IDockable Parent)>();
		var seen = new HashSet<IDockable>();

		queue.Enqueue((root, null));

		while (queue.Count > 0)
		{
			var current = queue.Dequeue();

			if (!seen.Add(current.Dockable))
				continue;

			yield return current;

			if (current.Dockable is IDock dock)
			{
				foreach (var child in Children(dock))
					queue.Enqueue((child, dock));
			}
		}
	}
}
