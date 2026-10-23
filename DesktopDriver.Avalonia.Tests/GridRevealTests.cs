namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia.Controls;
using global::Avalonia.Data;
using global::Avalonia.Headless;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.States;

/// <summary>
/// A record brought onto the screen can be pressed there.
/// </summary>
/// <remarks>
/// A grid scrolls a record far down only as far as its last line, and a grid whose columns are wider than itself draws its
/// horizontal bar over that line: the record was on the screen and under the bar, and pressing it was refused as covered.
/// The order book of a depth panel is such a grid.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class GridRevealTests : BaseTestClass
{
	private sealed class Row
	{
		public string Name { get; init; }
	}

	private static Task RunAsync(Action body) => AssemblyInitializer.Session.Dispatch(body, CancellationToken.None);

	[TestMethod]
	[Timeout(60000)]
	public Task ARecordBroughtOntoTheScreenStandsClearOfTheBarDrawnOverTheRows() => RunAsync(() =>
	{
		// Wider columns than the grid, so that it has a horizontal bar.
		var grid = new DataGrid
		{
			Name = "Rows",
			AutoGenerateColumns = false,
			GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
			HeadersVisibility = DataGridHeadersVisibility.Column,
			Width = 180,
			Height = 167,
			ItemsSource = Enumerable.Range(0, 200).Select(index => new Row { Name = $"Row {index}" }).ToArray(),
		};

		grid.Columns.Add(new DataGridTextColumn { ColumnKey = "Name", Header = "Name", Binding = new Binding(nameof(Row.Name)), Width = new DataGridLength(400) });

		using var harness = new ControlHarness(grid);

		var subject = harness.SubjectOf(grid);
		var adapter = (IUiInputTargetAdapter)harness.Adapters.Resolve(subject);

		foreach (var index in new[] { 37L, 73L, 111L, 150L, 180L, 199L })
		{
			var key = harness.GridAdapter
				.ReadRows(subject, new GridRowsQuery(GridRowsModes.ViewData, index, [], [], new UiPageRequest(1, null), null), harness.ContextOf(grid))
				.Items[0].Key;

			var part = new UiGridCellPart(key, "Name");

			adapter.ResolveInputTarget(subject, part, UiEnsureVisibleAction.Instance, harness.ContextOf(grid));
			harness.Settle();
			harness.Window.CaptureRenderedFrame()?.Dispose();

			// Refused as covered when the record stands under the bar.
			try
			{
				adapter.ResolveInputTarget(subject, part, new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.None), harness.ContextOf(grid));
			}
			catch (UiAutomationException error)
			{
				Fail($"Record {index} cannot be pressed: {error.Message}");
			}
		}
	});
}
