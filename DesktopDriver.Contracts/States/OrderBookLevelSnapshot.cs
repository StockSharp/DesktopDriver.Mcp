namespace StockSharp.DesktopDriver.States;

using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// One price level of an order book.
/// </summary>
/// <param name="Key">The level's stable key, so a test can name it rather than count rows.</param>
/// <param name="Side">Which side it is on.</param>
/// <param name="RowIndex">Its position in the ladder as the book is showing it.</param>
/// <param name="Price">The price.</param>
/// <param name="Volume">How much is on offer at it.</param>
/// <param name="OrderCount">How many orders make it up, when the feed says.</param>
/// <param name="IsBest">Whether it is the best price on its side.</param>
/// <param name="IsOwn">Whether the trader has an order of their own at it.</param>
/// <param name="BoundsInSurfaceDip">Where the row is, when it has been drawn.</param>
/// <remarks>
/// The price and the volume are the aggregation the book is actually showing, not the raw feed: grouping
/// levels together is a thing a book does, and a test that compared against the feed would pass while the
/// screen showed something else.
/// </remarks>
public sealed record OrderBookLevelSnapshot(
	string Key,
	OrderBookSides Side,
	long RowIndex,
	decimal Price,
	decimal Volume,
	UiField<long> OrderCount,
	bool IsBest,
	bool IsOwn,
	UiField<UiRect> BoundsInSurfaceDip);
