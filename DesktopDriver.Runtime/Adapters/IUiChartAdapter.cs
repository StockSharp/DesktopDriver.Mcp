namespace StockSharp.DesktopDriver.Adapters;

using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.States;

/// <summary>
/// Reads a chart: its series and their points.
/// </summary>
public interface IUiChartAdapter : IUiSnapshotAdapter
{
	/// <summary>
	/// Reads the series.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="query">Which series.</param>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>One page of series.</returns>
	UiDataPage<ChartSeriesSnapshot> ReadSeries(
		UiSubject subject,
		ChartSeriesQuery query,
		UiCaptureContext context);

	/// <summary>
	/// Reads the points of one series.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="query">Which points.</param>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>One page of points.</returns>
	UiDataPage<ChartPointSnapshot> ReadPoints(
		UiSubject subject,
		ChartPointsQuery query,
		UiCaptureContext context);
}
