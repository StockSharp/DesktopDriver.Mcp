namespace StockSharp.DesktopDriver.Avalonia;

using System;
using System.Collections.Immutable;
using System.Linq;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Presenters;
using global::Avalonia.VisualTree;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Reads a tree: what is inside what, what is open and what is selected.
/// </summary>
/// <remarks>
/// An item is named by its path through the tree rather than by a position, because a position changes
/// with every item opened above it. Two siblings that read the same get a number.
/// <para>
/// Nothing here opens an item. A tree builds what is inside an item when it is opened, so asking for it
/// would be opening it - and a closed item is usually what the test is about.
/// </para>
/// </remarks>
public class AvaloniaTreeAdapter(AvaloniaNodeBinder binder)
	: AvaloniaControlAdapter(binder), IUiTreeAdapter, IUiInputTargetAdapter
{
	/// <inheritdoc />
	public override string Kind => "tree";

	/// <inheritdoc />
	public override bool CanHandle(UiSubject subject) => subject?.Instance is TreeView;

	/// <inheritdoc />
	public override ImmutableArray<string> GetCapabilities(UiSubject subject)
		=> ["input.click", "input.key", "input.scroll", "tree.items"];

	/// <inheritdoc />
	public bool SupportsTargetPart(UiSubject subject, UiTargetPart part)
		=> CanHandle(subject) && part is UiTreeItemPart or UiTreeItemControlPart;

	/// <inheritdoc />
	/// <remarks>
	/// Only an item on show can be reached: one inside a closed item has not been built, and opening it to
	/// reach it would change the tree the input was meant for.
	/// </remarks>
	public UiResolvedInputTarget ResolveInputTarget(
		UiSubject subject,
		UiTargetPart part,
		UiInputAction action,
		UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var tree = (TreeView)subject.Instance;

		var (key, controlName) = part switch
		{
			UiTreeItemPart item => (item.ItemKey, (string)null),
			UiTreeItemControlPart inside => (inside.ItemKey, inside.ControlName),
			_ => throw UiErrors.Unsupported($"A tree has no part called {part?.Kind}."),
		};

		var entry = AvaloniaTreeItems.Shown(tree).FirstOrDefault(candidate => candidate.Key == key);

		if (entry.Item is null)
			throw UiErrors.NotFound($"'{key}' is not on show in this tree.");

		if (action is UiEnsureVisibleAction)
		{
			entry.Item.BringIntoView();
			tree.UpdateLayout();
		}

		// The item's own row: everything inside it that belongs to no item nested in it.
		var row = entry.Item
			.GetVisualDescendants()
			.OfType<ContentPresenter>()
			.FirstOrDefault(presenter => presenter.Name == "PART_HeaderPresenter" && presenter.FindAncestorOfType<TreeViewItem>() == entry.Item)
			?? throw UiErrors.Fail(UiErrorCodes.NotCreated, $"'{key}' has not drawn its row yet.");

		if (controlName is null)
			return AvaloniaInputTargetResolver.Locate(row, context?.Node, context?.Revisions, $"the row of '{key}'");

		var control = row
			.GetVisualDescendants()
			.OfType<Control>()
			.FirstOrDefault(candidate => candidate.Name == controlName)
			?? throw UiErrors.NotFound($"The row of '{key}' holds nothing called '{controlName}'.");

		return AvaloniaInputTargetResolver.Locate(control, context?.Node, context?.Revisions, $"'{controlName}' of '{key}'");
	}

	/// <inheritdoc />
	public override UiStateCapture CaptureState(UiSubject subject, UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var tree = (TreeView)subject.Instance;
		var shown = AvaloniaTreeItems.Shown(tree).ToArray();
		var selected = shown.Where(entry => entry.Item.IsSelected).ToArray();

		return new UiStateCapture(
			new TreeState(
				UiField<long>.Known(tree.ItemCount),
				UiField<long>.Known(shown.Length),
				UiField<long>.Known(shown.Count(entry => entry.Item.IsExpanded)),
				selected.Length == 1
					? UiField<string>.Known(selected[0].Key)
					: UiField<string>.Known(null),
				UiField<long>.Known(selected.Length),
				tree.SelectionMode.HasFlag(SelectionMode.Multiple)),
			UiReadyStatuses.Ready,
			UiCompleteness.Complete,
			[]);
	}

	/// <inheritdoc />
	/// <remarks>
	/// A tree holds items, not nodes. They are read with tree.items, where they come with their paths,
	/// their text and whether they are open.
	/// </remarks>
	public override UiDataPage<UiChildLink> ReadChildren(
		UiSubject subject,
		UiPageRequest query,
		UiCaptureContext context)
		=> new(context?.Node, null, [], UiField<long>.Known(0), false, null, null);

	/// <inheritdoc />
	public UiDataPage<TreeItemSnapshot> ReadItems(
		UiSubject subject,
		TreeItemsQuery query,
		UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var tree = (TreeView)subject.Instance;
		var wanted = query?.Keys ?? [];
		var parent = query?.ParentKey;
		var items = ImmutableArray.CreateBuilder<TreeItemSnapshot>();
		var window = new UiPageWindow(query?.Page, "tree.items", Describe(query), "items");

		foreach (var entry in AvaloniaTreeItems.Shown(tree))
		{
			if (!string.IsNullOrEmpty(parent) && entry.ParentKey != parent)
				continue;

			if (!wanted.IsDefaultOrEmpty && !wanted.Contains(entry.Key))
				continue;

			if (!window.Take())
				continue;

			var text = AvaloniaTreeItems.TextOf(entry.Item);

			items.Add(new TreeItemSnapshot(
				entry.Key,
				entry.ParentKey,
				entry.Depth,
				text is null
					? UiField<string>.Unavailable(
						UiUnavailableReasons.Unsupported, "This item shows something that is not text.")
					: UiField<string>.Known(text),
				entry.Item.IsExpanded,
				entry.Item.IsSelected,
				entry.Item.ItemCount > 0,
				UiField<long>.Known(entry.Item.ItemCount),
				Bounds(entry.Item, tree)));
		}

		return new UiDataPage<TreeItemSnapshot>(
			context?.Node,
			null,
			items.ToImmutable(),
			UiField<long>.Known(window.Total),
			window.Truncated,
			window.NextCursor,
			window.TruncationReason);
	}

	// What the page is a page of. A cursor issued for one set of items must not continue another: the
	// two would arrive as one list and nothing in the answer would show the join.
	private static string Describe(TreeItemsQuery query)
		=> $"parent={query?.ParentKey};keys={string.Join(',', query?.Keys ?? [])}";

	private static UiField<UiRect> Bounds(TreeViewItem item, TreeView tree)
	{
		var bounds = item.Bounds;

		if (bounds.Width <= 0 || bounds.Height <= 0)
			return UiField<UiRect>.Unavailable(UiUnavailableReasons.NotCreated, "This item has not been drawn.");

		var offset = item.TranslatePoint(default, tree);

		return offset is null
			? UiField<UiRect>.Unavailable(UiUnavailableReasons.NotCreated, "This item is not on the tree right now.")
			: UiField<UiRect>.Known(new UiRect(offset.Value.X, offset.Value.Y, bounds.Width, bounds.Height));
	}
}
