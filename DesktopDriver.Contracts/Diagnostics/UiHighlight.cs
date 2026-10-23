namespace StockSharp.DesktopDriver.Diagnostics;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;

/// <summary>
/// One thing a picture points at: a frame is drawn round it.
/// </summary>
/// <param name="Node">What it belongs to.</param>
/// <param name="Part">Which part of it, or <see langword="null"/> for the whole of it.</param>
/// <remarks>
/// A part is named the same way an action or a mask names one, and answered by the same adapter. The frame stands a
/// little outside what it points at, so that it covers none of it, and is drawn into the picture once the picture is
/// taken: the application never shows it.
/// </remarks>
public sealed record UiHighlight(UiNodeId Node, UiTargetPart Part);
