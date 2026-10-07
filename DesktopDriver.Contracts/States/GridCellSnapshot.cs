namespace StockSharp.DesktopDriver.States;

using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// One cell of a grid.
/// </summary>
/// <param name="ColumnId">Which column it is in.</param>
/// <param name="Value">Its value, typed.</param>
/// <param name="DisplayText">What it shows, when a visual exists to show it.</param>
/// <param name="ValueOrigin">Where the value came from.</param>
/// <param name="DisplayTextOrigin">Where the text came from.</param>
/// <param name="BoundsInSurfaceDip">Where it is, when it is on screen.</param>
public sealed record GridCellSnapshot(
	string ColumnId,
	UiField<UiValue> Value,
	UiField<string> DisplayText,
	UiDataOrigins ValueOrigin,
	UiField<UiDataOrigins> DisplayTextOrigin,
	UiField<UiRect> BoundsInSurfaceDip);
