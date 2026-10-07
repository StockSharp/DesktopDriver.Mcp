namespace StockSharp.DesktopDriver.States;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// One column of a grid.
/// </summary>
/// <param name="Id">The column's stable identifier.</param>
/// <param name="Header">What its header shows.</param>
/// <param name="ValueKind">The kind of value its cells carry.</param>
/// <param name="DisplayIndex">Where it sits left to right.</param>
/// <param name="IsVisible">Whether it is shown at all.</param>
/// <param name="WidthDip">How wide it is.</param>
/// <param name="IsFrozen">Whether it stays put while the grid scrolls sideways.</param>
public sealed record GridColumnSnapshot(
	string Id,
	string Header,
	string ValueKind,
	int DisplayIndex,
	bool IsVisible,
	UiField<double> WidthDip,
	UiField<bool> IsFrozen);
