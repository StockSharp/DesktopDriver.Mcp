namespace StockSharp.DesktopDriver.Mcp;

using StockSharp.DesktopDriver.Diagnostics;

/// <summary>
/// A picture the application took, and where this server put it.
/// </summary>
/// <param name="Info">What the application says about it.</param>
/// <param name="Path">The file it was written to.</param>
public sealed record UiPictureInfo(UiScreenshotInfo Info, string Path);
