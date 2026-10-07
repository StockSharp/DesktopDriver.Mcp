namespace StockSharp.DesktopDriver.States;

using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// One item of a tree.
/// </summary>
/// <param name="Key">The item's path through the tree, which is what a test names it by.</param>
/// <param name="ParentKey">The item it sits inside, or <see langword="null"/> at the top level.</param>
/// <param name="Depth">How deep it is.</param>
/// <param name="DisplayText">What it reads as.</param>
/// <param name="IsExpanded">Whether it is open.</param>
/// <param name="IsSelected">Whether it is selected.</param>
/// <param name="HasChildren">Whether it holds anything.</param>
/// <param name="ChildCount">How many items it holds.</param>
/// <param name="BoundsInSurfaceDip">Where it is, when it has been drawn.</param>
/// <remarks>
/// The path is the key rather than a position, because a position changes with every item opened above
/// it. Two siblings that read the same get a number - "Errors#2" - so that naming one of them still
/// names one of them.
/// </remarks>
public sealed record TreeItemSnapshot(
	string Key,
	string ParentKey,
	int Depth,
	UiField<string> DisplayText,
	bool IsExpanded,
	bool IsSelected,
	bool HasChildren,
	UiField<long> ChildCount,
	UiField<UiRect> BoundsInSurfaceDip);
