namespace StockSharp.DesktopDriver.Queries;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Which columns of a grid to read.
/// </summary>
/// <param name="ColumnIds">The columns; empty means all of them.</param>
/// <param name="Page">How many to return.</param>
/// <param name="Guard">What the caller believed about the grid.</param>
public sealed record GridColumnsQuery(
	ImmutableArray<string> ColumnIds,
	UiPageRequest Page,
	UiReadGuard Guard);
