namespace StockSharp.DesktopDriver.States;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// What is selected, without serialising a selection of any size.
/// </summary>
/// <param name="Count">How many items are selected.</param>
/// <param name="SampleKeys">Some of their keys.</param>
/// <param name="Truncated">Whether the sample is shorter than the selection.</param>
public sealed record UiSelectionSummary(
	UiField<long> Count,
	ImmutableArray<string> SampleKeys,
	bool Truncated)
{
	/// <summary>
	/// Nothing is selected.
	/// </summary>
	public static UiSelectionSummary Empty { get; } =
		new(UiField<long>.Known(0), ImmutableArray<string>.Empty, false);
}
