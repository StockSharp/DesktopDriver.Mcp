namespace StockSharp.DesktopDriver.States;

using System.Collections.Immutable;

/// <summary>
/// One row of a grid, as the grid is currently showing it.
/// </summary>
/// <param name="Key">The record's stable key.</param>
/// <param name="RowKind">Whether it is a record, a group header, a total or a placeholder.</param>
/// <param name="ViewIndex">Its position in the view that was asked for.</param>
/// <param name="GroupPath">The groups it sits inside, outermost first.</param>
/// <param name="Cells">The cells that were asked for.</param>
public sealed record GridRowSnapshot(
	string Key,
	GridRowKinds RowKind,
	long ViewIndex,
	ImmutableArray<string> GroupPath,
	ImmutableArray<GridCellSnapshot> Cells);
