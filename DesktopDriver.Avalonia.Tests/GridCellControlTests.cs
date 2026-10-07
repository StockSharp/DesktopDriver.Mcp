namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Templates;
using global::Avalonia.VisualTree;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.States;

/// <summary>
/// A control that lives inside a cell - the picker button beside an instrument, the "..." of a mapping.
/// </summary>
/// <remarks>
/// A cell holding an editor is one cell to the grid and two things to a person: the box and the button
/// beside it. A click at the middle of the cell lands in the box, so the button can only be reached by
/// naming it.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class GridCellControlTests : BaseTestClass
{
	private sealed class Row
	{
		public string Name { get; init; }
	}

	private static Task RunAsync(Action body) => AssemblyInitializer.Session.Dispatch(body, CancellationToken.None);

	private static DataGrid NewGrid()
	{
		var column = new DataGridTemplateColumn
		{
			ColumnKey = "Security",
			Width = new DataGridLength(260),
			CellTemplate = new FuncDataTemplate<Row>((_, _) =>
			{
				var button = new Button { Name = "PickerButton", Content = "...", Width = 30 };
				var panel = new DockPanel();

				DockPanel.SetDock(button, Dock.Right);

				panel.Children.Add(button);
				panel.Children.Add(new TextBox { Name = "Box" });

				return panel;
			}),
		};

		var grid = new DataGrid
		{
			Name = "Rows",
			AutoGenerateColumns = false,
			ItemsSource = new[] { new Row { Name = "First" } },
		};

		grid.Columns.Add(column);

		return grid;
	}

	[TestMethod]
	[Timeout(60000)]
	public Task AControlInsideACellIsWhereTheClickLands() => RunAsync(() =>
	{
		var grid = NewGrid();

		using var harness = new ControlHarness(grid);

		var subject = harness.SubjectOf(grid);
		var adapter = (IUiInputTargetAdapter)harness.Adapters.Resolve(subject);

		var rowKey = harness.GridAdapter
			.ReadRows(subject, new GridRowsQuery(GridRowsModes.ViewData, null, [], [], new UiPageRequest(1, null), null), harness.ContextOf(grid))
			.Items[0].Key;

		var part = new UiGridCellControlPart(rowKey, "Security", "PickerButton");

		IsTrue(adapter.SupportsTargetPart(subject, part));

		var target = adapter.ResolveInputTarget(subject, part, new UiClickAction(UiPointerButtons.Left, 1), harness.ContextOf(grid));

		var button = grid.GetVisualDescendants().OfType<Button>().Single(candidate => candidate.Name == "PickerButton");
		var origin = button.TranslatePoint(default, harness.Window) ?? throw new InvalidOperationException("The button is not laid out.");

		AreEqual(origin.X, target.BoundsInSurfaceDip.X, 0.5);
		AreEqual(origin.Y, target.BoundsInSurfaceDip.Y, 0.5);
		AreEqual(button.Bounds.Width, target.BoundsInSurfaceDip.Width, 0.5);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AControlTheCellDoesNotHoldIsNotFound() => RunAsync(() =>
	{
		var grid = NewGrid();

		using var harness = new ControlHarness(grid);

		var subject = harness.SubjectOf(grid);
		var adapter = (IUiInputTargetAdapter)harness.Adapters.Resolve(subject);

		var rowKey = harness.GridAdapter
			.ReadRows(subject, new GridRowsQuery(GridRowsModes.ViewData, null, [], [], new UiPageRequest(1, null), null), harness.ContextOf(grid))
			.Items[0].Key;

		var error = Throws<UiAutomationException>(() => adapter.ResolveInputTarget(
			subject,
			new UiGridCellControlPart(rowKey, "Security", "NoSuchButton"),
			new UiClickAction(UiPointerButtons.Left, 1),
			harness.ContextOf(grid)));

		AreEqual(UiErrorCodes.NotFound, error.Error.Code);
	});
}
