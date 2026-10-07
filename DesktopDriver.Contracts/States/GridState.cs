namespace StockSharp.DesktopDriver.States;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// What a grid holds and how it is showing it.
/// </summary>
/// <param name="Counts">How many records, at each stage.</param>
/// <param name="ColumnCount">How many columns.</param>
/// <param name="Sorts">The sort, in priority order.</param>
/// <param name="Groupings">The grouping, outermost first.</param>
/// <param name="Filter">The filter that is set.</param>
/// <param name="Selection">What is selected.</param>
/// <param name="CurrentCell">Where the cursor is; a known <see langword="null"/> means nowhere.</param>
/// <param name="Editing">Whether an editor is open.</param>
/// <remarks>
/// Rows are not here. A grid is asked for its rows separately, with a page and a guard, because a state
/// that carried them would be unbounded and a summary that carried some of them would be a lie about the
/// rest.
/// </remarks>
public sealed record GridState(
	GridCounts Counts,
	UiField<long> ColumnCount,
	ImmutableArray<GridSort> Sorts,
	ImmutableArray<GridGrouping> Groupings,
	UiField<UiFilter> Filter,
	UiSelectionSummary Selection,
	UiField<GridCellRef> CurrentCell,
	UiField<GridEditState> Editing) : UiState
{
	/// <inheritdoc />
	public override string Kind => UiStateKinds.Grid;
}
