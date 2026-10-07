namespace StockSharp.DesktopDriver.States;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// One series of a chart.
/// </summary>
/// <param name="Id">The series identifier.</param>
/// <param name="SeriesKind">Its registered kind.</param>
/// <param name="PlotAreaId">The area it is drawn in.</param>
/// <param name="XAxisId">The horizontal axis it is measured against.</param>
/// <param name="YAxisId">The vertical axis it is measured against.</param>
/// <param name="IsEnabled">Whether the chart is showing it.</param>
/// <param name="PointCount">How many points it has.</param>
/// <param name="NonGapPointCount">How many of them carry a value.</param>
/// <param name="ViewportPointCount">How many fall inside the current view.</param>
/// <remarks>
/// A series that is enabled and has points is not a series the user can see: the view may be elsewhere,
/// and having data is not the same as having drawn it.
/// </remarks>
public sealed record ChartSeriesSnapshot(
	string Id,
	string SeriesKind,
	string PlotAreaId,
	string XAxisId,
	string YAxisId,
	bool IsEnabled,
	UiField<long> PointCount,
	UiField<long> NonGapPointCount,
	UiField<long> ViewportPointCount);
