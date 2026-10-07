namespace StockSharp.DesktopDriver.Avalonia.Grids;

using System.Collections.Generic;
using System.Linq;

using global::Avalonia.Controls;
using global::Avalonia.Data;

/// <summary>
/// What a column reads off a record.
/// </summary>
/// <remarks>
/// Normally the column says so itself, in the path it sorts by. A column bound with a compiled binding
/// never fills that in - the path is turned into code at build time and nothing is left on the column to
/// read - and the products here are built with compiled bindings throughout. So what the column is bound
/// to, and failing that the name the grid knows it by, are tried as well.
/// </remarks>
internal static class GridColumnMembers
{
	/// <summary>
	/// Reads what a column shows off one record.
	/// </summary>
	/// <param name="column">The column.</param>
	/// <param name="id">The name the grid knows the column by.</param>
	/// <param name="item">The record.</param>
	/// <param name="value">What was there.</param>
	/// <returns><see langword="true"/> when one of the column's paths led to a value.</returns>
	public static bool TryRead(DataGridColumn column, string id, object item, out object value)
	{
		foreach (var path in Paths(column, id))
		{
			if (GridMembers.TryRead(item, path, out value))
				return true;
		}

		value = null;

		return false;
	}

	/// <summary>
	/// What was tried, for the answer when none of it worked.
	/// </summary>
	/// <param name="column">The column.</param>
	/// <param name="id">The name the grid knows the column by.</param>
	/// <returns>The paths, as one phrase.</returns>
	public static string Describe(DataGridColumn column, string id)
	{
		var paths = Paths(column, id).ToArray();

		return paths.Length == 0 ? "anything" : string.Join(" or ", paths);
	}

	/// <summary>
	/// Whether a column reads a path.
	/// </summary>
	/// <param name="column">The column.</param>
	/// <param name="id">The name the grid knows the column by.</param>
	/// <param name="path">The path a sort or a grouping names.</param>
	/// <returns><see langword="true"/> when this column is the one that path belongs to.</returns>
	public static bool Reads(DataGridColumn column, string id, string path)
		=> Paths(column, id).Contains(path);

	private static IEnumerable<string> Paths(DataGridColumn column, string id)
	{
		if (!string.IsNullOrEmpty(column?.SortMemberPath))
			yield return column.SortMemberPath;

		if (column is DataGridBoundColumn bound && PathOf(bound.Binding) is { } bindingPath)
			yield return bindingPath;

		if (!string.IsNullOrEmpty(id))
			yield return id;
	}

	private static string PathOf(object binding)
	{
		var path = binding switch
		{
			Binding classic => classic.Path,
			CompiledBinding compiled => compiled.Path?.ToString(),
			_ => null,
		};

		// A compiled path writes itself the way it would be written in markup, which starts at the record
		// rather than at a member of it.
		return string.IsNullOrEmpty(path) ? null : path.TrimStart('.');
	}
}
