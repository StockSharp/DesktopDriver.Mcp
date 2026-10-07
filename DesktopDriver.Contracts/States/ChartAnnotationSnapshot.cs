namespace StockSharp.DesktopDriver.States;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// One annotation drawn over a chart.
/// </summary>
/// <param name="Id">The annotation identifier.</param>
/// <param name="AnnotationKind">Its registered kind.</param>
/// <param name="PlotAreaId">The area it belongs to.</param>
/// <param name="Coordinates">Its position in the chart's own units.</param>
/// <param name="PreparedBoundsInSurfaceDip">Where the chart prepared to draw it.</param>
public sealed record ChartAnnotationSnapshot(
	string Id,
	string AnnotationKind,
	string PlotAreaId,
	ImmutableDictionary<string, UiValue> Coordinates,
	UiField<UiRect> PreparedBoundsInSurfaceDip);
