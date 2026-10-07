namespace StockSharp.DesktopDriver.Avalonia.Grids;

using System;
using System.Collections.Generic;
using System.Linq;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.VisualTree;

/// <summary>
/// The parts of a grid that actually exist on screen right now.
/// </summary>
/// <remarks>
/// A grid builds visuals only for what it is showing, plus a few either side to scroll into. The
/// difference matters for every question about what a person can see: rows kept ready for scrolling are
/// not visible, and rows in a collapsed group are not there at all.
/// </remarks>
public static class GridVisuals
{
	/// <summary>
	/// The rows the grid has actually built.
	/// </summary>
	/// <param name="grid">The grid.</param>
	/// <returns>The rows, in the order the grid holds them.</returns>
	public static IReadOnlyList<DataGridRow> RealizedRows(DataGrid grid)
		=> [.. grid.GetVisualDescendants().OfType<DataGridRow>().Where(row => row.IsVisible)];

	/// <summary>
	/// The rows that fall inside the area the grid is showing.
	/// </summary>
	/// <param name="grid">The grid.</param>
	/// <returns>The rows, topmost first.</returns>
	public static IReadOnlyList<DataGridRow> ViewportRows(DataGrid grid)
	{
		var viewport = Viewport(grid);

		if (viewport is not { } area)
			return [];

		return
		[
			.. RealizedRows(grid)
				.Select(row => (Row: row, Bounds: AvaloniaVisuals.BoundsIn(row, grid)))
				.Where(pair => pair.Bounds is { } bounds && bounds.Intersects(area))
				.OrderBy(pair => pair.Bounds.Value.Y)
				.Select(pair => pair.Row),
		];
	}

	/// <summary>
	/// The area of the grid the rows are drawn in.
	/// </summary>
	/// <param name="grid">The grid.</param>
	/// <returns>The area, in the grid's own coordinates.</returns>
	public static Rect? Viewport(DataGrid grid)
	{
		ArgumentNullException.ThrowIfNull(grid);

		var presenter = grid.GetVisualDescendants()
			.OfType<Control>()
			.FirstOrDefault(control => control.GetType().Name == "DataGridRowsPresenter");

		if (presenter is null)
			return grid.Bounds.Width > 0 && grid.Bounds.Height > 0 ? new Rect(grid.Bounds.Size) : null;

		return AvaloniaVisuals.BoundsIn(presenter, grid);
	}

	/// <summary>
	/// The text a built cell is showing.
	/// </summary>
	/// <param name="content">The cell's content.</param>
	/// <returns>The text, or nothing when the cell shows something that is not text.</returns>
	/// <remarks>
	/// All of it, in the order it is drawn: a name with what was searched for picked out is several pieces side
	/// by side. The caption of a button in the cell - the arrow that folds a row of a tree - is the button's,
	/// not the cell's, and text that is not shown is no part of it.
	/// </remarks>
	public static string TextOf(Control content)
	{
		if (content is null)
			return null;

		if (content is TextBlock block)
			return block.Text;

		var pieces = content
			.GetVisualDescendants()
			.OfType<TextBlock>()
			.Where(text => text.IsEffectivelyVisible && !text.GetVisualAncestors().TakeWhile(ancestor => ancestor != content).OfType<Button>().Any())
			.Select(text => text.Text)
			.ToArray();

		if (pieces.Length > 0)
			return string.Concat(pieces);

		return content is ContentControl { Content: { } value } ? value.ToString() : null;
	}
}
