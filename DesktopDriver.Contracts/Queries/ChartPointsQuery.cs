namespace StockSharp.DesktopDriver.Queries;

using System;
using System.Collections.Immutable;

using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Which points of a series to read.
/// </summary>
/// <param name="SeriesId">The series.</param>
/// <param name="StartIndex">Where to start in the series.</param>
/// <param name="FromInclusive">The start of a time range, UTC.</param>
/// <param name="ToExclusive">The end of that range, UTC and exclusive.</param>
/// <param name="PointKeys">Particular points, by key.</param>
/// <param name="Page">How many to return.</param>
/// <param name="Guard">What the caller believed about the chart.</param>
/// <remarks>
/// One way of choosing at a time: an index, a time range, a set of keys, or a cursor. A series with no
/// time axis refuses a time range instead of reading the timestamps as indices.
/// </remarks>
public sealed record ChartPointsQuery(
	string SeriesId,
	long? StartIndex,
	DateTime? FromInclusive,
	DateTime? ToExclusive,
	ImmutableArray<string> PointKeys,
	UiPageRequest Page,
	UiReadGuard Guard);
