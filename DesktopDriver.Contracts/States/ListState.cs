namespace StockSharp.DesktopDriver.States;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// What a list is showing.
/// </summary>
/// <param name="ItemCount">How many items it holds.</param>
/// <param name="SelectedIndex">Which item is selected, or -1 when none is.</param>
/// <param name="SelectedCount">How many are selected.</param>
/// <param name="AllowsMultipleSelection">Whether more than one may be selected at a time.</param>
/// <remarks>
/// What it holds rather than what has been laid out: a list of a thousand items builds a handful of
/// them and recycles the rest as the reader scrolls, so counting the built ones would answer with the
/// height of the window instead of the size of the list.
/// </remarks>
public sealed record ListState(
	UiField<long> ItemCount,
	UiField<long> SelectedIndex,
	UiField<long> SelectedCount,
	bool AllowsMultipleSelection) : UiState
{
	/// <inheritdoc />
	public override string Kind => UiStateKinds.List;
}
