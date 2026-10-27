namespace StockSharp.DesktopDriver.States;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// A socket of a block of a diagram.
/// </summary>
/// <param name="Id">The socket's id: what a connection names its ends by, and what a test aims at.</param>
/// <param name="DisplayText">What the socket is called.</param>
/// <param name="IsInput">Whether the block takes data through it; otherwise the block gives data through it.</param>
public sealed record DiagramSocketSnapshot(
	string Id,
	UiField<string> DisplayText,
	bool IsInput);
