namespace StockSharp.DesktopDriver.Cli;

using StockSharp.DesktopDriver.Diagnostics;

/// <summary>
/// A picture the application took, and where it was put.
/// </summary>
/// <param name="Info">What the application says about it.</param>
/// <param name="Path">Where it was written, or <see langword="null"/> when it was not asked for.</param>
/// <remarks>
/// The picture itself is not printed. It stays an artifact until somebody asks for the bytes, because a
/// megabyte of base64 on a pipe is not something a caller wants by accident.
/// </remarks>
public sealed record UiCapturedScreenshot(UiScreenshotInfo Info, string Path);
