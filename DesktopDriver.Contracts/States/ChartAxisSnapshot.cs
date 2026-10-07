namespace StockSharp.DesktopDriver.States;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// One axis of a chart.
/// </summary>
/// <param name="Id">The axis identifier.</param>
/// <param name="ScaleKind">Its registered scale kind.</param>
/// <param name="Minimum">The low end of its current range.</param>
/// <param name="Maximum">The high end of its current range.</param>
/// <param name="IsAutoRange">Whether it follows the data.</param>
public sealed record ChartAxisSnapshot(
	string Id,
	string ScaleKind,
	UiField<UiValue> Minimum,
	UiField<UiValue> Maximum,
	bool IsAutoRange);
