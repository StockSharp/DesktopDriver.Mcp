namespace StockSharp.DesktopDriver.States;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// How many records a grid has, at each stage between the source and the screen.
/// </summary>
/// <param name="Source">In the bound collection.</param>
/// <param name="Loaded">Actually fetched, when the source pages.</param>
/// <param name="Filtered">Left after filtering.</param>
/// <param name="RealizedDataRows">Data rows that exist as visuals.</param>
/// <param name="ViewportDataRows">Data rows inside the visible area.</param>
/// <remarks>
/// These are five different numbers and they are meant to differ. A grid that has fetched a thousand of a
/// million records, kept two hundred after a filter and realised twelve of them is working correctly, and
/// only these five separate answers can say so.
/// </remarks>
public sealed record GridCounts(
	UiField<long> Source,
	UiField<long> Loaded,
	UiField<long> Filtered,
	UiField<long> RealizedDataRows,
	UiField<long> ViewportDataRows);
