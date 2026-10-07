namespace StockSharp.DesktopDriver.Avalonia.Grids;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using global::Avalonia.Collections;
using global::Avalonia.Controls;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Avalonia;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Reads a <see cref="DataGrid"/>: its columns, the rows it is showing, and its groups.
/// </summary>
/// <remarks>
/// Registered for <see cref="DataGrid"/> itself, so every table an application builds on it is read by
/// this one adapter. A derived table adds columns and behaviour, not a different way of being a table;
/// one that is a different thing - an order book, say - derives an adapter from this one.
/// <para>
/// Everything comes from the view the grid is actually showing, after its sorting, filtering and
/// grouping. A test that checked a sort against the collection the grid is bound to would be checking
/// the collection, and would pass with the sort broken.
/// </para>
/// </remarks>
public class DataGridAdapter(AvaloniaNodeBinder binder)
	: AvaloniaControlAdapter(binder), IUiGridAdapter, IUiInputTargetAdapter
{
	private const int _sampleKeys = 32;

	/// <inheritdoc />
	public override string Kind => "grid";

	/// <inheritdoc />
	public override bool CanHandle(UiSubject subject) => subject?.Instance is DataGrid;

	/// <inheritdoc />
	public bool SupportsTargetPart(UiSubject subject, UiTargetPart part)
		=> subject?.Instance is DataGrid && part is UiGridHeaderPart or UiGridCellPart or UiGridCellControlPart;

	/// <inheritdoc />
	public UiResolvedInputTarget ResolveInputTarget(
		UiSubject subject,
		UiTargetPart part,
		UiInputAction action,
		UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var grid = (DataGrid)subject.Instance;

		if (action is UiEnsureVisibleAction)
		{
			// Done before the visual is looked for, because until the view has moved there is no visual to
			// find: a record a thousand rows down has no place on screen at all.
			switch (part)
			{
				case UiGridHeaderPart header:
					GridParts.Show(grid, null, header.ColumnId);

					break;

				case UiGridCellPart cell:
					GridParts.Show(grid, cell.RowKey, cell.ColumnId);

					break;

				case UiGridCellControlPart control:
					GridParts.Show(grid, control.RowKey, control.ColumnId);

					break;
			}
		}

		var (visual, what) = part switch
		{
			UiGridHeaderPart header => (GridParts.Header(grid, header.ColumnId), $"'{header.ColumnId}' header"),
			UiGridCellPart cell => (GridParts.Cell(grid, cell.RowKey, cell.ColumnId), $"'{cell.ColumnId}' cell"),
			UiGridCellControlPart control => (
				GridParts.CellControl(grid, control.RowKey, control.ColumnId, control.ControlName),
				$"'{control.ControlName}' of the '{control.ColumnId}' cell"),
			_ => throw UiErrors.Unsupported($"A grid has no part called {part?.Kind}."),
		};

		return AvaloniaInputTargetResolver.Locate(visual, context?.Node, context?.Revisions, what);
	}

	/// <inheritdoc />
	public override ImmutableArray<string> GetCapabilities(UiSubject subject)
	{
		var grid = (DataGrid)subject.Instance;
		var capabilities = ImmutableArray.CreateBuilder<string>();

		capabilities.Add("input.click");
		capabilities.Add("input.key");
		capabilities.Add("grid.columns");
		capabilities.Add("grid.rows");

		// Of this grid, not of grids in general: one that is not grouped has no groups to read, and
		// saying it has would send a test looking for them.
		if (grid.CollectionView?.IsGrouping == true)
			capabilities.Add("grid.groups");

		return capabilities.ToImmutable();
	}

	/// <inheritdoc />
	public override UiStateCapture CaptureState(UiSubject subject, UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var grid = (DataGrid)subject.Instance;
		var keys = UiRowKeys.For(grid);

		return new UiStateCapture(
			new GridState(
				GridViewReader.Counts(grid),
				UiField<long>.Known(grid.Columns.Count),
				GridViewReader.Sorts(grid),
				GridViewReader.Groupings(grid),
				GridViewReader.Filter(grid),
				GridViewReader.Selection(grid, keys, _sampleKeys),
				GridViewReader.CurrentCell(grid, keys),
				GridViewReader.Editing(grid, keys)),
			grid.CollectionView is null ? UiReadyStatuses.Loading : UiReadyStatuses.Ready,
			UiCompleteness.Complete,
			[]);
	}

	/// <inheritdoc />
	/// <remarks>
	/// A grid holds records, not nodes. Its rows are read with grid.rows, where they come with keys, a
	/// page and the order the grid is showing them in; putting them in the tree instead would bury the
	/// rest of the application under one table.
	/// </remarks>
	public override UiDataPage<UiChildLink> ReadChildren(
		UiSubject subject,
		UiPageRequest query,
		UiCaptureContext context)
		=> new(context?.Node, null, [], UiField<long>.Known(0), false, null, null);

	/// <inheritdoc />
	public UiDataPage<GridColumnSnapshot> ReadColumns(
		UiSubject subject,
		GridColumnsQuery query,
		UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var grid = (DataGrid)subject.Instance;
		var page = query?.Page ?? UiPageRequest.Default;
		var wanted = query?.ColumnIds ?? [];
		var columns = ImmutableArray.CreateBuilder<GridColumnSnapshot>();
		var window = new UiPageWindow(query?.Page, "grid.columns", DescribeColumns(query), "columns");

		for (var index = 0; index < grid.Columns.Count; index++)
		{
			var column = grid.Columns[index];
			var id = GridColumnIdentity.Of(column, index);

			if (!wanted.IsDefaultOrEmpty && !wanted.Contains(id))
				continue;

			if (!window.Take())
				continue;

			columns.Add(new GridColumnSnapshot(
				id,
				column.Header?.ToString(),
				ValueKindOf(column),
				column.DisplayIndex,
				column.IsVisible,
				column.ActualWidth > 0
					? UiField<double>.Known(column.ActualWidth)
					: UiField<double>.Unavailable(UiUnavailableReasons.NotCreated, "The column has not been measured."),
				UiField<bool>.Known(column.IsFrozen)));
		}

		return new UiDataPage<GridColumnSnapshot>(
			context?.Node,
			null,
			columns.ToImmutable(),
			UiField<long>.Known(window.Total),
			window.Truncated,
			window.NextCursor,
			window.TruncationReason);
	}

	/// <inheritdoc />
	public UiDataPage<GridRowSnapshot> ReadRows(UiSubject subject, GridRowsQuery query, UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);
		ArgumentNullException.ThrowIfNull(query);

		var grid = (DataGrid)subject.Instance;
		var keys = UiRowKeys.For(grid);
		var page = query.Page ?? UiPageRequest.Default;
		var columns = SelectedColumns(grid, query.ColumnIds);
		var rows = ImmutableArray.CreateBuilder<GridRowSnapshot>();
		var realized = RealizedByItem(grid);
		var records = Records(grid, query, keys, out var total);
		var window = new UiPageWindow(page, "grid.rows", DescribeRows(grid, query), "rows");

		foreach (var (item, viewIndex) in records)
		{
			if (!window.Take())
				continue;

			realized.TryGetValue(item, out var row);

			rows.Add(new GridRowSnapshot(
				keys.KeyOf(item),
				GridRowKinds.Data,
				viewIndex,
				GroupPathOf(grid, item),
				[.. columns.Select(column => ReadCell(grid, column, item, row))]));
		}

		return new UiDataPage<GridRowSnapshot>(
			context?.Node,
			null,
			rows.ToImmutable(),
			total,
			window.Truncated,
			window.NextCursor,
			window.TruncationReason);
	}

	/// <inheritdoc />
	public UiDataPage<GridGroupSnapshot> ReadGroups(
		UiSubject subject,
		GridGroupsQuery query,
		UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var grid = (DataGrid)subject.Instance;

		if (grid.CollectionView is not { IsGrouping: true } view)
		{
			throw UiErrors.Unsupported(
				"This grid is not grouped, so it has no groups; grouping it is something a person does.");
		}

		var page = query?.Page ?? UiPageRequest.Default;
		var expanded = ExpandedGroups(grid);
		var groups = ImmutableArray.CreateBuilder<GridGroupSnapshot>();
		var parent = query?.ParentGroupId;
		var window = new UiPageWindow(query?.Page, "grid.groups", DescribeGroups(query), "groups");

		foreach (var (group, id, parentId, level) in Walk(view.Groups, null, 0))
		{
			if (!string.Equals(parentId, parent, StringComparison.Ordinal))
				continue;

			if (!window.Take())
				continue;

			groups.Add(new GridGroupSnapshot(
				id,
				parentId,
				UiValues.From(group.Key),
				level,
				expanded.GetValueOrDefault(id, true),
				UiField<long>.Known(group.ItemCount),
				ImmutableDictionary<string, UiField<UiValue>>.Empty));
		}

		return new UiDataPage<GridGroupSnapshot>(
			context?.Node,
			null,
			groups.ToImmutable(),
			UiField<long>.Known(window.Total),
			window.Truncated,
			window.NextCursor,
			window.TruncationReason);
	}

	private static IEnumerable<(DataGridCollectionViewGroup Group, string Id, string ParentId, int Level)> Walk(
		IEnumerable groups,
		string parentId,
		int level)
	{
		if (groups is null)
			yield break;

		var index = 0;

		foreach (var candidate in groups)
		{
			if (candidate is not DataGridCollectionViewGroup group)
				continue;

			var id = parentId is null ? $"g{index}" : $"{parentId}/{index}";

			index++;

			yield return (group, id, parentId, level);

			if (!group.IsBottomLevel)
			{
				foreach (var nested in Walk(group.Items, id, level + 1))
					yield return nested;
			}
		}
	}

	private static Dictionary<string, bool> ExpandedGroups(DataGrid grid)
	{
		var states = new Dictionary<string, bool>(StringComparer.Ordinal);

		try
		{
			var captured = grid.CaptureGroupingState(new DataGridStateOptions());

			if (captured?.GroupStates is null)
				return states;

			foreach (var state in captured.GroupStates)
				states[string.Join("/", state.PathKeys.Select(key => key?.ToString()))] = state.IsExpanded;
		}
		catch (Exception)
		{
			// A grid that cannot describe its groups' state still lists the groups themselves.
		}

		return states;
	}

	private static ImmutableArray<string> GroupPathOf(DataGrid grid, object item)
	{
		if (grid.CollectionView is not { IsGrouping: true } view)
			return [];

		var path = ImmutableArray.CreateBuilder<string>();

		for (var level = 0; level < view.GroupingDepth; level++)
		{
			var group = grid.GetGroupFromItem(item, level);

			if (group is null)
				break;

			path.Add(group.Key?.ToString());
		}

		return path.ToImmutable();
	}

	private static GridCellSnapshot ReadCell(DataGrid grid, DataGridColumn column, object item, DataGridRow row)
	{
		var id = GridColumnIdentity.Of(column, grid.Columns.IndexOf(column));
		var value = GridColumnMembers.TryRead(column, id, item, out var read)
			? UiField<UiValue>.Known(UiValues.From(read))
			: UiField<UiValue>.Unavailable(
				UiUnavailableReasons.Unsupported,
				$"Nothing on this record answers to {GridColumnMembers.Describe(column, id)}.");

		if (row is null)
		{
			// The record is in the view but the grid has not drawn it, which is the normal state of most
			// of a long table. Its value is still readable; what it looks like is not.
			return new GridCellSnapshot(
				id,
				value,
				UiField<string>.Unavailable(UiUnavailableReasons.NotCreated, "This row is not on screen."),
				UiDataOrigins.ViewModelSource,
				UiField<UiDataOrigins>.Unavailable(UiUnavailableReasons.NotCreated, null),
				UiField<UiRect>.Unavailable(UiUnavailableReasons.NotCreated, null));
		}

		var content = column.GetCellContent(row);
		var text = GridVisuals.TextOf(content);

		return new GridCellSnapshot(
			id,
			value,
			text is null
				? UiField<string>.Unavailable(UiUnavailableReasons.Unsupported, "This cell shows something that is not text.")
				: UiField<string>.Known(text),
			UiDataOrigins.ViewModelSource,
			UiField<UiDataOrigins>.Known(UiDataOrigins.RealizedVisual),
			AvaloniaVisuals.BoundsIn(content, grid) is { } bounds
				? UiField<UiRect>.Known(new UiRect(bounds.X, bounds.Y, bounds.Width, bounds.Height))
				: UiField<UiRect>.Unavailable(UiUnavailableReasons.NotCreated, null));
	}

	private static IReadOnlyList<DataGridColumn> SelectedColumns(DataGrid grid, ImmutableArray<string> wanted)
	{
		if (wanted.IsDefaultOrEmpty)
			return [.. grid.Columns];

		var byId = GridColumnIdentity.Index(grid);

		return [.. wanted.Select(id => byId.GetValueOrDefault(id)).Where(column => column is not null)];
	}

	private static Dictionary<object, DataGridRow> RealizedByItem(DataGrid grid)
	{
		var rows = new Dictionary<object, DataGridRow>(ReferenceEqualityComparer.Instance);

		foreach (var row in GridVisuals.RealizedRows(grid))
		{
			if (row.DataContext is { } item)
				rows[item] = row;
		}

		return rows;
	}

	private IEnumerable<(object Item, long ViewIndex)> Records(
		DataGrid grid,
		GridRowsQuery query,
		UiRowKeys keys,
		out UiField<long> total)
	{
		if (query.Mode == GridRowsModes.Viewport)
		{
			var visible = GridVisuals.ViewportRows(grid);

			total = UiField<long>.Known(visible.Count);

			return [.. visible.Select(row => (row.DataContext, (long)row.Index))];
		}

		var view = grid.CollectionView as DataGridCollectionView;

		if (view is null)
		{
			total = UiField<long>.Unavailable(UiUnavailableReasons.Unsupported, "This grid has no view to read.");

			return [];
		}

		total = UiField<long>.Known(view.Count);

		if (!query.RowKeys.IsDefaultOrEmpty)
		{
			// Asked for by key, answered in the order the grid is showing them: a reply in the order they
			// were asked for would hide a sort that is wrong.
			var wanted = query.RowKeys.ToHashSet(StringComparer.Ordinal);

			return [.. All(view).Where(record => wanted.Contains(keys.KeyOf(record.Item)))];
		}

		var start = query.StartIndex ?? 0;

		return All(view).Where(record => record.ViewIndex >= start);
	}

	private static IEnumerable<(object Item, long ViewIndex)> All(DataGridCollectionView view)
	{
		for (var index = 0; index < view.Count; index++)
			yield return (view.GetItemAt(index), index);
	}

	// Named by what the column reads rather than by how it draws it: a test asserts on the value, and a
	// column that renders a number as a progress bar still holds a number.
	private static string ValueKindOf(DataGridColumn column) => column switch
	{
		DataGridCheckBoxColumn => UiValueKinds.Boolean,
		DataGridNumericColumn => UiValueKinds.Decimal,
		DataGridDatePickerColumn or DataGridTimePickerColumn => UiValueKinds.Timestamp,
		_ => UiValueKinds.String,
	};
	// What each page is a page of. A cursor issued before a re-sort must not continue after it: the rows
	// would come from two different orders and arrive looking like one list.
	private static string DescribeColumns(GridColumnsQuery query)
		=> $"columns={string.Join(',', query?.ColumnIds ?? [])}";

	private static string DescribeGroups(GridGroupsQuery query)
		=> $"parent={query?.ParentGroupId}";

	// What the page is a page of. A cursor issued before a sort must not continue after one: the rows
	// would come from two different orders and arrive looking like a single list. The grid's own view
	// state is part of it for that reason, not only the query - it is what decides which rows there are.
	private static string DescribeRows(DataGrid grid, GridRowsQuery query)
		=> $"mode={query?.Mode};start={query?.StartIndex};keys={string.Join(',', query?.RowKeys ?? [])};" +
			$"columns={string.Join(',', query?.ColumnIds ?? [])};" +
			$"sorts={string.Join(',', GridViewReader.Sorts(grid).Select(sort => $"{sort.ColumnId}:{sort.Direction}"))};" +
			$"groups={string.Join(',', GridViewReader.Groupings(grid).Select(group => group.ColumnId))};" +
			$"filter={GridViewReader.Filter(grid)}";
}
