namespace StockSharp.DesktopDriver.States;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// One drawing area of a chart.
/// </summary>
/// <param name="Id">The area identifier.</param>
/// <param name="BoundsInSurfaceDip">Where it is.</param>
/// <param name="AxisIds">The axes it is measured against.</param>
public sealed record ChartPlotAreaSnapshot(
	string Id,
	UiField<UiRect> BoundsInSurfaceDip,
	ImmutableArray<string> AxisIds);
