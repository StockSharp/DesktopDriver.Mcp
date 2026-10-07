namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia.Controls;
using global::Avalonia.Controls.Templates;
using global::Avalonia.Layout;
using global::Avalonia.Media;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// The text a cell drawn by a template of its own shows.
/// </summary>
/// <remarks>
/// A name with what was searched for picked out is drawn as several pieces of text side by side, and a row of a
/// tree has a button to fold it in front of its name. The cell was read as its first piece of text - the fold
/// button's arrow, or the first letters of the name - so a case looking for the name never found it.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class GridCellTextTests : BaseTestClass
{
	private sealed class Row
	{
		public string Name { get; init; }
	}

	private static Task RunAsync(Action body) => AssemblyInitializer.Session.Dispatch(body, CancellationToken.None);

	[TestMethod]
	[Timeout(60000)]
	public Task ACellReadsAsAllTheTextItShowsButNotItsButtons() => RunAsync(() =>
	{
		var column = new DataGridTemplateColumn
		{
			ColumnKey = "Name",
			Width = new DataGridLength(260),
			CellTemplate = new FuncDataTemplate<Row>((_, _) =>
			{
				var panel = new StackPanel { Orientation = Orientation.Horizontal };

				panel.Children.Add(new Button { Content = "▾" });
				panel.Children.Add(new TextBlock { Text = "GA", FontWeight = FontWeight.Bold });
				panel.Children.Add(new TextBlock { Text = "ZP@TQBR" });
				panel.Children.Add(new TextBlock { Text = "hidden", IsVisible = false });

				return panel;
			}),
		};

		var grid = new DataGrid
		{
			Name = "Rows",
			AutoGenerateColumns = false,
			ItemsSource = new[] { new Row { Name = "GAZP@TQBR" } },
		};

		grid.Columns.Add(column);

		using var harness = new ControlHarness(grid);

		var row = harness.GridAdapter
			.ReadRows(harness.SubjectOf(grid), new GridRowsQuery(GridRowsModes.ViewData, null, [], [], new UiPageRequest(1, null), null), harness.ContextOf(grid))
			.Items
			.Single();

		var text = row.Cells.Single(cell => cell.ColumnId == "Name").DisplayText;

		IsTrue(text is UiKnown<string> { Value: "GAZP@TQBR" }, $"The cell reads as {text}.");
	});
}
