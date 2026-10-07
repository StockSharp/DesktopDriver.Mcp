namespace StockSharp.DesktopDriver.Identity;

/// <summary>
/// What a reply says about a node: its address, the live visual behind it when there is one, and its kind.
/// </summary>
/// <param name="Id">The meaning-level address.</param>
/// <param name="Handle">The live visual, or <see langword="null"/> when nothing has been created yet.</param>
/// <param name="Kind">The registered node kind, such as <c>grid</c> or <c>button</c>.</param>
public sealed record UiNodeRef(UiNodeId Id, UiHandle Handle, string Kind);
