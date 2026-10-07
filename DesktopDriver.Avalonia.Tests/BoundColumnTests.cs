namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia.Controls;
using global::Avalonia.Data;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// What a column reads off a record, when the column does not say so in the one place a reader looks.
/// </summary>
/// <remarks>
/// A grid column is asked what it sorts by, and that is normally also what it shows. A column bound with
/// a compiled binding - which is what every product here is built with - never fills that in, so a table
/// full of visible values answered that nothing on the record answers to anything.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class BoundColumnTests : BaseTestClass
{
	private sealed class Row
	{
		public string Name { get; init; }

		public decimal Price { get; init; }
	}

	private static readonly Row[] _rows =
	[
		new() { Name = "First", Price = 10.5m },
		new() { Name = "Second", Price = 20.25m },
	];

	private static Task RunAsync(Action body) => AssemblyInitializer.Session.Dispatch(body, CancellationToken.None);

	private static DataGrid NewGrid(IEnumerable<DataGridColumn> columns)
	{
		var grid = new DataGrid
		{
			Name = "Rows",
			AutoGenerateColumns = false,
			ItemsSource = _rows,
		};

		foreach (var column in columns)
			grid.Columns.Add(column);

		return grid;
	}

	private static UiValue ValueOf(ControlHarness harness, DataGrid grid, string columnId)
	{
		var adapter = (IUiGridAdapter)harness.Adapters.Resolve(harness.SubjectOf(grid));

		var rows = adapter.ReadRows(
			harness.SubjectOf(grid),
			new GridRowsQuery(GridRowsModes.ViewData, null, [], [columnId], new UiPageRequest(10, null), null),
			harness.ContextOf(grid));

		var cell = rows.Items[0].Cells[0];

		return cell.Value is UiKnown<UiValue> known
			? known.Value
			: throw new InvalidOperationException(
				$"The column answered nothing: {((UiUnavailable<UiValue>)cell.Value).Detail} " +
				$"(binding {((DataGridBoundColumn)grid.Columns[0]).Binding?.GetType().FullName}, " +
				$"path '{((DataGridBoundColumn)grid.Columns[0]).Binding}')");
	}

	[TestMethod]
	[Timeout(60000)]
	public Task AColumnBoundWithACompiledBindingIsStillRead() => RunAsync(() =>
	{
		// The binding every product here is built with. It fills in nothing about what it sorts by, so a
		// reader that only asks that question reads an entire table of nothing.
		var view = new CompiledBindingGrid();

		using var harness = new ControlHarness(view);

		AreEqual("First", ((UiStringValue)ValueOf(harness, view.Grid, "TheName")).Value);
		AreEqual(10.5m, ((UiDecimalValue)ValueOf(harness, view.Grid, "ThePrice")).Value);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AColumnThatSortsByNothingIsReadByWhatItIsBoundTo() => RunAsync(() =>
	{
		// The same hole reached the other way: a column that says nothing about sorting still knows what
		// it shows, and that is enough to read it.
		var column = new DataGridTextColumn
		{
			ColumnKey = "TheValue",
			Binding = new Binding(nameof(Row.Price)),
		};

		var grid = NewGrid([column]);

		using var harness = new ControlHarness(grid);

		column.SortMemberPath = null;

		AreEqual(10.5m, ((UiDecimalValue)ValueOf(harness, grid, "TheValue")).Value);
	});
}
