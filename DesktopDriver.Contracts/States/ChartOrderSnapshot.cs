namespace StockSharp.DesktopDriver.States;

using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// An order a chart shows: a line at its price, taken hold of to move the order, and a mark that cancels it.
/// </summary>
/// <param name="Id">What the chart calls it; its line and its cancel mark are aimed at by this.</param>
/// <param name="PlotAreaId">The area it is drawn in.</param>
/// <param name="Side">Which way it trades.</param>
/// <param name="Price">The price it is drawn at.</param>
/// <param name="Balance">What is left of its volume.</param>
/// <param name="BoundsInSurfaceDip">Where the part that is taken hold of is, once the chart has drawn it.</param>
public sealed record ChartOrderSnapshot(
	string Id,
	string PlotAreaId,
	ChartOrderSides Side,
	decimal Price,
	decimal Balance,
	UiField<UiRect> BoundsInSurfaceDip);
