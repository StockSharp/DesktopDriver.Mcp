namespace StockSharp.DesktopDriver.Api;

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Session;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Waiting;

/// <summary>
/// Everything that can be asked of a running interface.
/// </summary>
/// <remarks>
/// One interface, implemented twice: inside the application by the service, and across the channel by the
/// client. A test written against it runs both ways round without changing, and neither side can grow an
/// operation the other does not have.
/// </remarks>
public interface IUiAutomationApi
{
	/// <summary>
	/// Who answered, and what it can do.
	/// </summary>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The session.</returns>
	Task<UiSessionInfo> GetSessionAsync(CancellationToken cancellationToken);

	/// <summary>
	/// The windows and popups currently on screen.
	/// </summary>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The surfaces.</returns>
	Task<ImmutableArray<UiSurfaceInfo>> GetSurfacesAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Finds nodes matching a selector.
	/// </summary>
	/// <param name="query">What to look for.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>What was found.</returns>
	Task<UiFindResult> FindAsync(UiFindQuery query, CancellationToken cancellationToken);

	/// <summary>
	/// Walks the meaning-level tree.
	/// </summary>
	/// <param name="query">Where to start and how far to go.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The tree.</returns>
	Task<UiTreeSnapshot> GetTreeAsync(UiTreeQuery query, CancellationToken cancellationToken);

	/// <summary>
	/// Reads one node.
	/// </summary>
	/// <param name="target">The node.</param>
	/// <param name="options">How much to read.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The node.</returns>
	Task<UiNodeSnapshot> CaptureAsync(
		UiTarget target,
		UiCaptureOptions options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads the columns of a grid.
	/// </summary>
	/// <param name="target">The grid.</param>
	/// <param name="query">Which columns.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>One page of columns.</returns>
	Task<UiDataPage<GridColumnSnapshot>> ReadGridColumnsAsync(
		UiTarget target,
		GridColumnsQuery query,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads the rows of a grid.
	/// </summary>
	/// <param name="target">The grid.</param>
	/// <param name="query">Which rows.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>One page of rows.</returns>
	Task<UiDataPage<GridRowSnapshot>> ReadGridRowsAsync(
		UiTarget target,
		GridRowsQuery query,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads the groups of a grid.
	/// </summary>
	/// <param name="target">The grid.</param>
	/// <param name="query">Which groups.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>One page of groups.</returns>
	Task<UiDataPage<GridGroupSnapshot>> ReadGridGroupsAsync(
		UiTarget target,
		GridGroupsQuery query,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads the series of a chart.
	/// </summary>
	/// <param name="target">The chart.</param>
	/// <param name="query">Which series.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>One page of series.</returns>
	Task<UiDataPage<ChartSeriesSnapshot>> ReadChartSeriesAsync(
		UiTarget target,
		ChartSeriesQuery query,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads the points of one series.
	/// </summary>
	/// <param name="target">The chart.</param>
	/// <param name="query">Which points.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>One page of points.</returns>
	Task<UiDataPage<ChartPointSnapshot>> ReadChartPointsAsync(
		UiTarget target,
		ChartPointsQuery query,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads the price levels of an order book.
	/// </summary>
	/// <param name="target">The book.</param>
	/// <param name="query">Which levels.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>One page of levels.</returns>
	/// <remarks>
	/// Separate from the table's rows on purpose: a book is drawn as a table, but a level is a price and a
	/// volume on a side, and a caller that had to work that out of cells would be re-implementing the book.
	/// </remarks>
	Task<UiDataPage<OrderBookLevelSnapshot>> ReadOrderBookLevelsAsync(
		UiTarget target,
		OrderBookLevelsQuery query,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads the properties a property editor is showing.
	/// </summary>
	/// <param name="target">The editor.</param>
	/// <param name="query">Which properties.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>One page of properties.</returns>
	Task<UiDataPage<PropertyItemSnapshot>> ReadPropertyEditorItemsAsync(
		UiTarget target,
		PropertyEditorItemsQuery query,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads the items a tree is showing.
	/// </summary>
	/// <param name="target">The tree.</param>
	/// <param name="query">Which items.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>One page of items.</returns>
	Task<UiDataPage<TreeItemSnapshot>> ReadTreeItemsAsync(
		UiTarget target,
		TreeItemsQuery query,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads the lines of a document.
	/// </summary>
	/// <param name="target">The document.</param>
	/// <param name="query">Which lines.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>One page of lines.</returns>
	Task<UiDataPage<DocumentLineSnapshot>> ReadDocumentContentAsync(
		UiTarget target,
		DocumentContentQuery query,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads the blocks of a diagram.
	/// </summary>
	/// <param name="target">The diagram.</param>
	/// <param name="query">Which blocks.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>One page of blocks.</returns>
	Task<UiDataPage<DiagramNodeSnapshot>> ReadDiagramNodesAsync(
		UiTarget target,
		DiagramNodesQuery query,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads the connections of a diagram.
	/// </summary>
	/// <param name="target">The diagram.</param>
	/// <param name="query">Which connections.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>One page of connections.</returns>
	/// <remarks>
	/// Separate from the blocks on purpose: a diagram is read to find out what is joined to what, and a
	/// caller that had to work that out of two overlapping lists would do it differently in every test.
	/// </remarks>
	Task<UiDataPage<DiagramConnectionSnapshot>> ReadDiagramConnectionsAsync(
		UiTarget target,
		DiagramConnectionsQuery query,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads a docking layout.
	/// </summary>
	/// <param name="target">The workspace.</param>
	/// <param name="query">Which part of it.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The layout.</returns>
	Task<DockLayoutSnapshot> ReadDockLayoutAsync(
		UiTarget target,
		DockLayoutQuery query,
		CancellationToken cancellationToken);

	/// <summary>
	/// Does something to the interface.
	/// </summary>
	/// <param name="request">What to do.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>What became of it.</returns>
	Task<UiActionReceipt> ExecuteInputAsync(
		UiInputRequest request,
		CancellationToken cancellationToken);

	/// <summary>
	/// Asks again what became of an action.
	/// </summary>
	/// <param name="actionId">The caller's identifier for it.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>What became of it.</returns>
	Task<UiActionReceipt> GetActionStatusAsync(Guid actionId, CancellationToken cancellationToken);

	/// <summary>
	/// Waits until a node satisfies a condition.
	/// </summary>
	/// <param name="request">What to wait for.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>What was observed when it held.</returns>
	Task<UiWaitResult> WaitAsync(UiWaitRequest request, CancellationToken cancellationToken);

	/// <summary>
	/// Takes a picture of a node.
	/// </summary>
	/// <param name="request">What to picture and how.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>Where the picture is kept.</returns>
	Task<UiScreenshotInfo> CaptureScreenshotAsync(
		UiScreenshotRequest request,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads part of an artifact.
	/// </summary>
	/// <param name="request">Which part.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The bytes.</returns>
	Task<UiArtifactChunk> ReadArtifactAsync(
		UiArtifactReadRequest request,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads what the module noticed.
	/// </summary>
	/// <param name="query">Which entries.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>One page of entries.</returns>
	Task<UiDiagnosticPage> ReadDiagnosticsAsync(
		UiDiagnosticQuery query,
		CancellationToken cancellationToken);
}
