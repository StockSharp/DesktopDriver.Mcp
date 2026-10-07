namespace StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// A point in device independent pixels, relative to the surface the node is drawn on.
/// </summary>
/// <param name="X">Horizontal coordinate.</param>
/// <param name="Y">Vertical coordinate.</param>
public sealed record UiPoint(double X, double Y);
