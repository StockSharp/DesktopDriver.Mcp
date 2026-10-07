namespace StockSharp.DesktopDriver.Queries;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;

/// <summary>
/// Which rows of a grid to read.
/// </summary>
/// <param name="Mode">The records it has, or the rows on screen.</param>
/// <param name="StartIndex">Where to start in the view.</param>
/// <param name="RowKeys">Particular records, by key.</param>
/// <param name="ColumnIds">Which cells to fill in; empty means all columns.</param>
/// <param name="Page">How many to return.</param>
/// <param name="Guard">What the caller believed about the grid.</param>
/// <remarks>
/// A page starts one way only: an index, a set of keys, or a cursor. Accepting two of them would leave
/// the reply's meaning up to whichever the implementation happened to prefer.
/// </remarks>
public sealed record GridRowsQuery(
	GridRowsModes Mode,
	long? StartIndex,
	ImmutableArray<string> RowKeys,
	ImmutableArray<string> ColumnIds,
	UiPageRequest Page,
	UiReadGuard Guard);
