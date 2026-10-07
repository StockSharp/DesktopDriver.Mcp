namespace StockSharp.DesktopDriver.Queries;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// One page of a bounded read.
/// </summary>
/// <typeparam name="T">What the page carries.</typeparam>
/// <param name="Source">The node the page was read from.</param>
/// <param name="Stamp">When it was read and at what revision. The read pipeline fills this in.</param>
/// <param name="Items">The items.</param>
/// <param name="Total">How many there are in all, when that is knowable.</param>
/// <param name="Truncated">Whether a limit stopped the page short.</param>
/// <param name="NextCursor">Where to continue, when there is more.</param>
/// <param name="TruncationReason">Which limit stopped it.</param>
/// <remarks>
/// A total that the source cannot know - a server-paged grid, for instance - is reported as unavailable
/// rather than filled in with the number of rows fetched so far.
/// </remarks>
public sealed record UiDataPage<T>(
	UiNodeRef Source,
	UiSnapshotStamp Stamp,
	ImmutableArray<T> Items,
	UiField<long> Total,
	bool Truncated,
	string NextCursor,
	string TruncationReason) : IUiStamped
{
	/// <inheritdoc />
	public IUiStamped WithStamp(UiSnapshotStamp stamp) => this with { Stamp = stamp };
}
