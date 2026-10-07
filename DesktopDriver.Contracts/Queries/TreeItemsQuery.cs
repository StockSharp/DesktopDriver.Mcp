namespace StockSharp.DesktopDriver.Queries;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Which items of a tree to read.
/// </summary>
/// <param name="ParentKey">Inside this item; the whole tree when empty.</param>
/// <param name="Keys">The items wanted, or empty for all of them.</param>
/// <param name="Page">Which page.</param>
/// <param name="Guard">What the caller believes it is reading.</param>
/// <remarks>
/// What comes back is what the tree is showing. Nothing here opens an item: a tree builds what is inside
/// an item when it is opened, so asking for it would be opening it, and a closed item is usually what
/// the test is about.
/// </remarks>
public sealed record TreeItemsQuery(
	string ParentKey,
	ImmutableArray<string> Keys,
	UiPageRequest Page,
	UiReadGuard Guard);
