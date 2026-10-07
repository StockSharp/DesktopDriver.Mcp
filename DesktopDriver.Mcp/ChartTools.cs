namespace StockSharp.DesktopDriver.Mcp;

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using StockSharp.DesktopDriver.Queries;

/// <summary>
/// Tools that read a chart.
/// </summary>
/// <remarks>
/// What comes back is what was drawn, not what was fed in. A candle that never made it onto the chart
/// is not in the answer, and that is the point: a chart that quietly drew nothing is exactly the fault
/// worth catching.
/// </remarks>
[McpServerToolType]
public static class ChartTools
{
	[McpServerTool(Name = "ui_chart_series", Title = "A chart's series", ReadOnly = true)]
	[Description(
		"The series a chart is drawing: what each is, which area it is in, how many points it holds and " +
		"what it is drawn with. Read this first — the series identifiers it answers with are what names " +
		"a series to ui_chart_points.")]
	public static Task<string> SeriesAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("The chart, as scope/identifier.")] string node,
		[Description("How many to answer with; 50 by default.")] int limit = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var target = UiAnswers.Node(node);
		var query = new ChartSeriesQuery(UiAnswers.Page(limit, null), null);

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.ReadChartSeriesAsync(target, query, cancellationToken)));
	}

	[McpServerTool(Name = "ui_chart_points", Title = "A series' points", ReadOnly = true)]
	[Description(
		"The points of one series, as they were drawn. A period or a starting point narrows the answer, " +
		"which matters on a chart holding thousands of candles. Times are UTC.")]
	public static Task<string> PointsAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("The chart, as scope/identifier.")] string node,
		[Description("Which series, from ui_chart_series.")] string seriesId,
		[Description("Start at this point number; from the beginning when negative.")] long startIndex = -1,
		[Description("From this UTC time, as 2026-01-02T10:00:00Z.")] DateTime? fromInclusive = null,
		[Description("Up to but not including this UTC time.")] DateTime? toExclusive = null,
		[Description("Only these points, by key, separated by commas.")] string pointKeys = null,
		[Description("How many to answer with; 50 by default.")] int limit = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var target = UiAnswers.Node(node);
		var query = new ChartPointsQuery(
			seriesId,
			startIndex >= 0 ? startIndex : null,
			fromInclusive,
			toExclusive,
			UiAnswers.List(pointKeys),
			UiAnswers.Page(limit, null),
			null);

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.ReadChartPointsAsync(target, query, cancellationToken)));
	}
}
