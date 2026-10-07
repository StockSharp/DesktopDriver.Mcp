namespace StockSharp.DesktopDriver.Queries;

using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Which series of a chart to read.
/// </summary>
/// <param name="Page">How many to return.</param>
/// <param name="Guard">What the caller believed about the chart.</param>
public sealed record ChartSeriesQuery(UiPageRequest Page, UiReadGuard Guard);
