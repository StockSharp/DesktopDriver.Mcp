namespace StockSharp.DesktopDriver.Avalonia.Grids;

using System;
using System.Linq;

using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.VisualTree;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// Finds the visual behind a named part of a grid.
/// </summary>
/// <remarks>
/// A caller says "the Price header" or "the Volume cell of that trade". Where those are on screen is the
/// grid's own business and changes with every sort, scroll and resize, so it is answered here and never
/// by the caller computing coordinates.
/// </remarks>
internal static class GridParts
{
	/// <summary>
	/// The header of a column.
	/// </summary>
	/// <param name="grid">The grid.</param>
	/// <param name="columnId">The column.</param>
	/// <returns>The header visual.</returns>
	public static Control Header(DataGrid grid, string columnId)
	{
		var column = Column(grid, columnId);

		var header = grid.GetVisualDescendants()
			.OfType<DataGridColumnHeader>()
			.FirstOrDefault(candidate => ReferenceEquals(candidate.OwningColumn, column));

		return header ?? throw UiErrors.Fail(
			UiErrorCodes.NotCreated,
			$"The header of '{columnId}' is not built: the column is hidden or the grid shows no headers.");
	}

	/// <summary>
	/// The cell of a record in a column.
	/// </summary>
	/// <param name="grid">The grid.</param>
	/// <param name="rowKey">The record.</param>
	/// <param name="columnId">The column.</param>
	/// <returns>The cell visual.</returns>
	public static Control Cell(DataGrid grid, string rowKey, string columnId)
	{
		var column = Column(grid, columnId);
		var item = Item(grid, rowKey);

		var row = GridVisuals.RealizedRows(grid).FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, item))
			?? throw UiErrors.Fail(
				UiErrorCodes.NotCreated,
				$"'{rowKey}' has no visual: it is outside what the grid is showing. Ask for it to be brought " +
				"into view first, then look it up again - its position will have changed.");

		var cell = row.GetVisualDescendants()
			.OfType<DataGridCell>()
			.FirstOrDefault(candidate => ReferenceEquals(candidate.OwningColumn, column));

		return cell ?? throw UiErrors.Fail(
			UiErrorCodes.NotCreated,
			$"The '{columnId}' cell of '{rowKey}' is not built: the column is outside the part of the grid " +
			"that is drawn.");
	}

	/// <summary>
	/// A control the template of a cell names, such as the picker button beside an instrument.
	/// </summary>
	/// <param name="grid">The grid.</param>
	/// <param name="rowKey">The record.</param>
	/// <param name="columnId">The column.</param>
	/// <param name="name">The control's name in the cell's template.</param>
	/// <returns>The control.</returns>
	public static Control CellControl(DataGrid grid, string rowKey, string columnId, string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		var cell = Cell(grid, rowKey, columnId);

		return cell.GetVisualDescendants().OfType<Control>().FirstOrDefault(candidate => candidate.Name == name)
			?? throw UiErrors.Fail(UiErrorCodes.NotFound, $"The '{columnId}' cell of '{rowKey}' holds nothing called '{name}'.");
	}

	/// <summary>
	/// Brings a record onto the screen, in a column when one is named.
	/// </summary>
	/// <param name="grid">The grid.</param>
	/// <param name="rowKey">The record, or <see langword="null"/> for a column alone.</param>
	/// <param name="columnId">The column, or <see langword="null"/> for a record alone.</param>
	/// <remarks>
	/// The grid's own positioning, not the scrollbar. What this establishes is that the part is reachable,
	/// not that scrolling works - a test about scrolling sends a scroll.
	/// </remarks>
	public static void Show(DataGrid grid, string rowKey, string columnId)
	{
		var column = string.IsNullOrEmpty(columnId) ? null : Column(grid, columnId);
		var item = string.IsNullOrEmpty(rowKey) ? null : Item(grid, rowKey);

		grid.ScrollIntoView(item, column);
		grid.UpdateLayout();
	}

	private static DataGridColumn Column(DataGrid grid, string columnId)
	{
		if (GridColumnIdentity.Index(grid).TryGetValue(columnId ?? string.Empty, out var column))
			return column;

		throw UiErrors.Fail(UiErrorCodes.NotFound, $"This grid has no column called '{columnId}'.");
	}

	private static object Item(DataGrid grid, string rowKey)
	{
		if (UiRowKeys.For(grid).TryResolve(rowKey, out var item))
			return item;

		throw UiErrors.Fail(
			UiErrorCodes.StaleElement,
			$"'{rowKey}' names a record this grid no longer holds.");
	}
}
