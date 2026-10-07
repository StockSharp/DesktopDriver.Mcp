namespace StockSharp.DesktopDriver.Avalonia.Grids;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;

using global::Avalonia.Collections;
using global::Avalonia.Controls;
using global::Avalonia.Controls.DataGridFiltering;

using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Reads the view a grid is actually showing.
/// </summary>
/// <remarks>
/// The collection a grid is bound to is not an answer to what is on screen. Sorting, filtering and
/// grouping all happen between the two, and those are exactly what a test about a grid is checking, so
/// everything here comes from the grid's own view.
/// </remarks>
internal static class GridViewReader
{
	/// <summary>
	/// How many records there are, at each stage between the source and the screen.
	/// </summary>
	public static GridCounts Counts(DataGrid grid)
	{
		var view = grid.CollectionView;
		var source = Count(view?.SourceCollection ?? grid.ItemsSource);
		var filtered = view is DataGridCollectionView collection
			? UiField<long>.Known(collection.Count)
			: Count(view);

		return new GridCounts(
			source,
			// Everything a grid bound to a collection shows is already in memory. A grid that fetched pages from
			// somewhere would have to say how many it has, and would not be able to say how many there are.
			source,
			filtered,
			UiField<long>.Known(GridVisuals.RealizedRows(grid).Count),
			UiField<long>.Known(GridVisuals.ViewportRows(grid).Count));
	}

	/// <summary>
	/// The sort, most significant first.
	/// </summary>
	public static ImmutableArray<GridSort> Sorts(DataGrid grid)
	{
		var descriptions = grid.CollectionView?.SortDescriptions;

		if (descriptions is null || descriptions.Count == 0)
			return [];

		var sorts = ImmutableArray.CreateBuilder<GridSort>();

		for (var index = 0; index < descriptions.Count; index++)
		{
			var description = descriptions[index];

			sorts.Add(new GridSort(
				GridColumnIdentity.ForPath(grid, description.PropertyPath),
				description.Direction == ListSortDirection.Descending
					? UiSortDirections.Descending
					: UiSortDirections.Ascending,
				index));
		}

		return sorts.ToImmutable();
	}

	/// <summary>
	/// The grouping, outermost first.
	/// </summary>
	public static ImmutableArray<GridGrouping> Groupings(DataGrid grid)
	{
		if (grid.CollectionView is not DataGridCollectionView view || view.GroupDescriptions.Count == 0)
			return [];

		var groupings = ImmutableArray.CreateBuilder<GridGrouping>();

		for (var level = 0; level < view.GroupDescriptions.Count; level++)
		{
			var description = view.GroupDescriptions[level] as DataGridGroupDescription;

			groupings.Add(new GridGrouping(
				GridColumnIdentity.ForPath(grid, description?.PropertyName),
				level));
		}

		return groupings.ToImmutable();
	}

	/// <summary>
	/// The filter the grid is applying.
	/// </summary>
	/// <remarks>
	/// No filter is a known empty one, not an unknown. "Nothing is filtered out" and "nobody could tell"
	/// are different answers, and a test written against them says different things.
	/// </remarks>
	public static UiField<UiFilter> Filter(DataGrid grid)
	{
		var descriptors = grid.FilteringModel?.Descriptors;

		if (descriptors is null)
		{
			return grid.CollectionView?.Filter is null
				? UiField<UiFilter>.Known(new UiFilterGroup(UiBooleanOperators.All, []))
				: UiField<UiFilter>.Unavailable(
					UiUnavailableReasons.Unsupported,
					"This grid filters with a predicate of its own, which has no description to return.");
		}

		var items = ImmutableArray.CreateBuilder<UiFilter>();

		foreach (var descriptor in descriptors)
		{
			if (descriptor.HasPredicate || Operator(descriptor.Operator) is not { } comparison)
			{
				// Left unread rather than approximated: a filter reported as something close to the real
				// one would make a test pass against a grid that is showing different rows.
				return UiField<UiFilter>.Unavailable(
					UiUnavailableReasons.Unsupported,
					$"The filter on {descriptor.ColumnId} is a {descriptor.Operator} the protocol has no form for.");
			}

			items.Add(new UiFieldFilter(
				GridColumnIdentity.ForPath(grid, descriptor.PropertyPath) ?? descriptor.ColumnId?.ToString(),
				comparison,
				Arguments(descriptor)));
		}

		return UiField<UiFilter>.Known(new UiFilterGroup(UiBooleanOperators.All, items.ToImmutable()));
	}

	/// <summary>
	/// What is selected, by key.
	/// </summary>
	public static UiSelectionSummary Selection(DataGrid grid, UiRowKeys keys, int sampleLimit)
	{
		var selected = grid.SelectedItems;

		if (selected is null)
			return new UiSelectionSummary(UiField<long>.Known(0), [], false);

		var sample = ImmutableArray.CreateBuilder<string>();
		var count = 0L;

		foreach (var item in selected)
		{
			count++;

			if (sample.Count < sampleLimit)
				sample.Add(keys.KeyOf(item));
		}

		return new UiSelectionSummary(UiField<long>.Known(count), sample.ToImmutable(), count > sample.Count);
	}

	/// <summary>
	/// Where the cursor is.
	/// </summary>
	public static UiField<GridCellRef> CurrentCell(DataGrid grid, UiRowKeys keys)
	{
		var cell = grid.CurrentCell;

		if (!cell.IsValid || cell.Item is null || cell.Column is null)
			return UiField<GridCellRef>.Known(null);

		return UiField<GridCellRef>.Known(new GridCellRef(
			keys.KeyOf(cell.Item),
			GridColumnIdentity.Of(cell.Column, cell.ColumnIndex)));
	}

	/// <summary>
	/// Whether an editor is open, and on what.
	/// </summary>
	public static UiField<GridEditState> Editing(DataGrid grid, UiRowKeys keys)
	{
		if (grid.CollectionView is not DataGridCollectionView view)
			return UiField<GridEditState>.Unavailable(UiUnavailableReasons.Unsupported, "This grid has no view to edit in.");

		if (!view.IsEditingItem)
			return UiField<GridEditState>.Known(new GridEditState(false, UiField<GridCellRef>.Known(null), UiField<string>.Known(null), []));

		var cell = CurrentCell(grid, keys);
		var editor = cell is UiKnown<GridCellRef> { Value: { } reference }
			? EditorText(grid, reference)
			: UiField<string>.Unavailable(UiUnavailableReasons.NotCreated, "No cell is current.");

		return UiField<GridEditState>.Known(new GridEditState(true, cell, editor, []));
	}

	private static UiField<string> EditorText(DataGrid grid, GridCellRef reference)
	{
		var row = GridVisuals.RealizedRows(grid)
			.FirstOrDefault(candidate => UiRowKeys.For(grid).KeyOf(candidate.DataContext) == reference.RowKey);

		if (row is null)
			return UiField<string>.Unavailable(UiUnavailableReasons.NotCreated, "That row is not on screen.");

		var column = GridColumnIdentity.Index(grid).GetValueOrDefault(reference.ColumnId);
		var text = GridVisuals.TextOf(column?.GetCellContent(row));

		return text is null
			? UiField<string>.Unavailable(UiUnavailableReasons.NotCreated, "The editor has not been built.")
			: UiField<string>.Known(text);
	}

	private static ImmutableArray<UiValue> Arguments(FilteringDescriptor descriptor)
	{
		if (descriptor.Values is { Count: > 0 } values)
			return [.. values.Select(UiValues.From)];

		return descriptor.Value is null ? [] : [UiValues.From(descriptor.Value)];
	}

	private static UiFilterOperators? Operator(FilteringOperator value) => value switch
	{
		FilteringOperator.Equals => UiFilterOperators.Equal,
		FilteringOperator.NotEquals => UiFilterOperators.NotEqual,
		FilteringOperator.LessThan => UiFilterOperators.Less,
		FilteringOperator.LessThanOrEqual => UiFilterOperators.LessOrEqual,
		FilteringOperator.GreaterThan => UiFilterOperators.Greater,
		FilteringOperator.GreaterThanOrEqual => UiFilterOperators.GreaterOrEqual,
		FilteringOperator.Contains => UiFilterOperators.Contains,
		FilteringOperator.StartsWith => UiFilterOperators.StartsWith,
		FilteringOperator.In => UiFilterOperators.In,
		FilteringOperator.Between => UiFilterOperators.Between,
		_ => null,
	};

	private static UiField<long> Count(IEnumerable items) => items switch
	{
		null => UiField<long>.Unavailable(UiUnavailableReasons.Unsupported, "This grid is bound to nothing."),
		ICollection collection => UiField<long>.Known(collection.Count),
		_ => UiField<long>.Unavailable(
			UiUnavailableReasons.Unsupported,
			"Counting these records would mean walking all of them, which a read is not allowed to do."),
	};
}
