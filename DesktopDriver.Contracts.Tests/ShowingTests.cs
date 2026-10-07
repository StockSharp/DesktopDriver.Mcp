namespace StockSharp.DesktopDriver.Tests;

using System;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// The difference between a control that is there and a control that has something in it.
/// </summary>
/// <remarks>
/// A caller that only asks whether a panel exists cannot tell a blotter of thirty-nine orders from the
/// same blotter before anything arrived in it - and a picture of the second one looks exactly like a
/// picture of the first.
/// </remarks>
[TestClass]
public class ShowingTests : BaseTestClass
{
	[TestMethod]
	public void APanelWhoseOnlyTableIsEmptyIsShowingNothing()
	{
		var tree = Tree(Panel("OrdersPanel"), Grid("OrdersGrid", 0));

		AreEqual(0L, tree.Showing());
	}

	[TestMethod]
	public void APanelIsShowingWhatTheFullestThingInItHolds()
	{
		// The blotter beside a chart that has drawn nothing yet: the panel does have something to show, and
		// a rule that took the least of them would go on waiting for a picture already worth taking.
		var tree = Tree(Panel("OrdersPanel"), Chart("Sparkline", 0), Grid("OrdersGrid", 39));

		AreEqual(39L, tree.Showing());
	}

	[TestMethod]
	public void AControlThatCountsNothingIsNotEmpty()
	{
		// Null rather than zero: a button is not a table with no rows in it, and a caller that waits for
		// something to appear must not be told a button will never show anything.
		IsNull(Tree(Panel("BacktestWorkspace"), Panel("Toolbar")).Showing());
		IsNull(((UiState)null).Showing());
	}

	[TestMethod]
	public void AGridThatHasFetchedRowsNobodyFilteredCountsThem()
	{
		// What is left after filtering is what the reader sees, so it wins when both are known; a grid that
		// never filtered anything answers with what it fetched rather than with nothing.
		AreEqual(12L, Rows(12, null).Showing());
		AreEqual(3L, Rows(12, 3).Showing());
	}

	[TestMethod]
	public void EveryKindThatCountsAnythingAnswersTheSameQuestion()
	{
		AreEqual(5L, Tree(Chart("Candles", 5)).Showing());

		AreEqual(
			40L,
			new DocumentState(
				"code",
				UiField<string>.Known("cs"),
				UiField<long>.Known(40),
				UiField<long>.Known(1200),
				UiField<long>.Known(1),
				UiField<long>.Known(1),
				UiField<long>.Known(0),
				false,
				[]).Showing());
	}

	private static UiTreeSnapshot Tree(params UiNodeSnapshot[] nodes)
		=> new(Guid.Empty, Guid.Empty, [.. nodes], [], false, []);

	private static UiNodeSnapshot Grid(string id, long rows) => Node(id, "grid", Rows(rows, null));

	private static UiNodeSnapshot Chart(string id, long series) => Node(
		id,
		"chart",
		new ChartState(
			UiField<string>.Known("BTCUSDT@BNBFT"),
			UiField<string>.Known("00:15:00"),
			UiField<long>.Known(series),
			[],
			[],
			[],
			[],
			UiSelectionSummary.Empty));

	private static UiNodeSnapshot Panel(string id) => Node(id, "panel", null);

	private static GridState Rows(long loaded, long? filtered) => new(
		new GridCounts(
			UiField<long>.Known(loaded),
			UiField<long>.Known(loaded),
			filtered is { } value
				? UiField<long>.Known(value)
				: UiField<long>.Unavailable(UiUnavailableReasons.Unsupported, "nothing is filtered"),
			UiField<long>.Known(0),
			UiField<long>.Known(0)),
		UiField<long>.Known(4),
		[],
		[],
		UiField<UiFilter>.Known(null),
		UiSelectionSummary.Empty,
		UiField<GridCellRef>.Known(null),
		UiField<GridEditState>.Known(null));

	private static UiNodeSnapshot Node(string id, string kind, UiState state) => new(
		new UiNodeRef(new UiNodeId("window:MainWindow", id), null, kind),
		new UiSnapshotStamp(
			"1",
			Guid.Empty,
			Guid.Empty,
			DateTime.UtcNow,
			new UiRevisions(new UiRevision(Guid.Empty, 1), new UiRevision(Guid.Empty, 1), new UiRevision(Guid.Empty, 1)),
			UiConsistencies.UiThreadRead),
		new UiPresentation(
			UiContentStatuses.Created,
			UiField<string>.Known("MainWindow"),
			UiField<bool>.Known(true),
			UiField<bool>.Known(true),
			UiField<bool>.Known(true),
			UiField<bool>.Known(true),
			UiField<bool>.Known(false),
			UiField<UiRect>.Unavailable(UiUnavailableReasons.Unsupported, null),
			UiField<double>.Known(1)),
		UiReadyStatuses.Ready,
		[],
		state,
		UiCompleteness.Complete,
		[]);
}
