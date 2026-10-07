namespace StockSharp.DesktopDriver.States;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// What part of the data one drawing area is currently showing.
/// </summary>
/// <param name="PlotAreaId">The area.</param>
/// <param name="XMinimum">The left edge, in the horizontal axis's own units.</param>
/// <param name="XMaximum">The right edge.</param>
/// <param name="FirstVisibleIndex">The first point inside the view.</param>
/// <param name="LastVisibleIndex">The last point inside the view.</param>
public sealed record ChartViewportSnapshot(
	string PlotAreaId,
	UiField<UiValue> XMinimum,
	UiField<UiValue> XMaximum,
	UiField<long> FirstVisibleIndex,
	UiField<long> LastVisibleIndex);
