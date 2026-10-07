namespace StockSharp.DesktopDriver.States;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// What a tree is showing.
/// </summary>
/// <param name="RootCount">How many items it has at the top level.</param>
/// <param name="VisibleItemCount">How many items are on show, counting the ones inside opened items.</param>
/// <param name="ExpandedCount">How many of them are open.</param>
/// <param name="SelectedKey">The item that is selected, when one is.</param>
/// <param name="SelectedCount">How many are selected.</param>
/// <param name="AllowsMultipleSelection">Whether more than one may be selected at a time.</param>
/// <remarks>
/// On show means what a person can see by scrolling, not what the tree holds: an item inside a closed
/// one is not on show, and counting it would answer a question nobody asked.
/// </remarks>
public sealed record TreeState(
	UiField<long> RootCount,
	UiField<long> VisibleItemCount,
	UiField<long> ExpandedCount,
	UiField<string> SelectedKey,
	UiField<long> SelectedCount,
	bool AllowsMultipleSelection) : UiState
{
	/// <inheritdoc />
	public override string Kind => UiStateKinds.Tree;
}
