namespace StockSharp.DesktopDriver.Avalonia.Grids;

using System;
using System.Collections.Generic;

using global::Avalonia.Controls;

/// <summary>
/// What a column is called, so a test can name it.
/// </summary>
/// <remarks>
/// A grid that gives each column a key of its own uses that key to remember layouts, so it is the name
/// a column keeps when the user moves it, hides it or sorts by it.
/// A position would not survive any of those, and a header changes with the display language.
/// </remarks>
public static class GridColumnIdentity
{
	/// <summary>
	/// The identifier of a column.
	/// </summary>
	/// <param name="column">The column.</param>
	/// <param name="index">Its position, used only when it has nothing better.</param>
	/// <returns>The identifier.</returns>
	public static string Of(DataGridColumn column, int index)
	{
		if (column is null)
			return null;

		if (column.ColumnKey is { } key)
		{
			var text = key.ToString();

			if (!string.IsNullOrEmpty(text))
				return text;
		}

		// A column with no key of its own: named by what it reads, which at least stays the same when the
		// grid is rearranged.
		if (!string.IsNullOrEmpty(column.SortMemberPath))
			return column.SortMemberPath;

		return $"column[{index}]";
	}

	/// <summary>
	/// The columns of a grid, by their identifiers.
	/// </summary>
	/// <param name="grid">The grid.</param>
	/// <returns>The columns.</returns>
	public static IReadOnlyDictionary<string, DataGridColumn> Index(DataGrid grid)
	{
		ArgumentNullException.ThrowIfNull(grid);

		var columns = new Dictionary<string, DataGridColumn>(StringComparer.Ordinal);

		for (var index = 0; index < grid.Columns.Count; index++)
		{
			var column = grid.Columns[index];

			columns[Of(column, index)] = column;
		}

		return columns;
	}

	/// <summary>
	/// The column a property path belongs to, when one does.
	/// </summary>
	/// <param name="grid">The grid.</param>
	/// <param name="propertyPath">The path a sort or a grouping names.</param>
	/// <returns>The identifier, or the path itself when no column reads it.</returns>
	/// <remarks>
	/// A grid can be sorted or grouped by something it does not show. Saying so by returning the path is
	/// more useful than dropping the sort from the answer, which would make the answer look unsorted.
	/// </remarks>
	public static string ForPath(DataGrid grid, string propertyPath)
	{
		ArgumentNullException.ThrowIfNull(grid);

		if (string.IsNullOrEmpty(propertyPath))
			return null;

		for (var index = 0; index < grid.Columns.Count; index++)
		{
			var column = grid.Columns[index];

			var id = Of(column, index);

			if (GridColumnMembers.Reads(column, id, propertyPath))
				return id;
		}

		return propertyPath;
	}
}
