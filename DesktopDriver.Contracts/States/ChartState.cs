namespace StockSharp.DesktopDriver.States;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// What a chart is showing.
/// </summary>
/// <param name="InstrumentId">The instrument, when the chart is about one.</param>
/// <param name="TimeFrame">The time frame, when the chart has one.</param>
/// <param name="SeriesCount">How many series it holds.</param>
/// <param name="PlotAreas">Its drawing areas.</param>
/// <param name="Axes">Its axes and their ranges.</param>
/// <param name="Viewports">What part of the data each area is showing.</param>
/// <param name="AnnotationSample">Some of its annotations.</param>
/// <param name="Selection">What is selected on it.</param>
/// <remarks>
/// Points are not here, for the same reason rows are not in a grid's state: they are unbounded, and a
/// summary carrying some of them would misrepresent the rest.
/// </remarks>
public sealed record ChartState(
	UiField<string> InstrumentId,
	UiField<string> TimeFrame,
	UiField<long> SeriesCount,
	ImmutableArray<ChartPlotAreaSnapshot> PlotAreas,
	ImmutableArray<ChartAxisSnapshot> Axes,
	ImmutableArray<ChartViewportSnapshot> Viewports,
	ImmutableArray<ChartAnnotationSnapshot> AnnotationSample,
	UiSelectionSummary Selection) : UiState
{
	/// <inheritdoc />
	public override string Kind => UiStateKinds.Chart;
}
