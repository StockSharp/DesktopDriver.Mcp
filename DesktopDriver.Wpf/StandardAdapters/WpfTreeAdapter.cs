namespace StockSharp.DesktopDriver.Wpf;

using System;
using System.Collections.Immutable;
using System.Linq;

using System.Windows.Controls;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Reads a tree: what is inside what, what is open and what is selected.
/// </summary>
/// <param name="binder">Turns controls into registered nodes.</param>
/// <remarks>
/// An item is named by its path through the tree rather than by a position, because a position changes
/// with every item opened above it. Two siblings that read the same get a number.
/// <para>
/// Nothing here opens an item. A tree builds what is inside an item when it is opened, so asking for it
/// would be opening it - and a closed item is usually what the test is about.
/// </para>
/// </remarks>
public class WpfTreeAdapter(WpfNodeBinder binder) : WpfControlAdapter(binder), IUiTreeAdapter
{
	/// <inheritdoc />
	public override string Kind => "tree";

	/// <inheritdoc />
	public override bool CanHandle(UiSubject subject) => subject?.Instance is TreeView;

	/// <inheritdoc />
	public override ImmutableArray<string> GetCapabilities(UiSubject subject)
		=> ["input.click", "input.key", "input.scroll", "tree.items"];

	/// <inheritdoc />
	public override UiStateCapture CaptureState(UiSubject subject, UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var tree = (TreeView)subject.Instance;
		var shown = WpfTreeItems.Shown(tree).ToArray();
		var selected = shown.Where(entry => entry.Item.IsSelected).ToArray();

		return new UiStateCapture(
			new TreeState(
				UiField<long>.Known(tree.Items.Count),
				UiField<long>.Known(shown.Length),
				UiField<long>.Known(shown.Count(entry => entry.Item.IsExpanded)),
				selected.Length == 1
					? UiField<string>.Known(selected[0].Key)
					: UiField<string>.Known(null),
				UiField<long>.Known(selected.Length),
				// A tree holds one selected item at a time and offers no mode that widens it.
				false),
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

		foreach (var entry in WpfTreeItems.Shown(tree))
		{
			if (!string.IsNullOrEmpty(parent) && entry.ParentKey != parent)
				continue;

			if (!wanted.IsDefaultOrEmpty && !wanted.Contains(entry.Key))
				continue;

			if (!window.Take())
				continue;

			var text = WpfTreeItems.TextOf(entry.Item);

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
				entry.Item.Items.Count > 0,
				UiField<long>.Known(entry.Item.Items.Count),
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
		var size = item.RenderSize;

		if (size.Width <= 0 || size.Height <= 0)
			return UiField<UiRect>.Unavailable(UiUnavailableReasons.NotCreated, "This item has not been drawn.");

		// TransformToAncestor throws when the item is not below the tree, so ask before transforming.
		if (!item.IsDescendantOf(tree))
			return UiField<UiRect>.Unavailable(UiUnavailableReasons.NotCreated, "This item is not on the tree right now.");

		return item.TransformToAncestor(tree).TryTransform(default, out var offset)
			? UiField<UiRect>.Known(new UiRect(offset.X, offset.Y, size.Width, size.Height))
			: UiField<UiRect>.Unavailable(UiUnavailableReasons.NotCreated, "This item is not on the tree right now.");
	}
}
