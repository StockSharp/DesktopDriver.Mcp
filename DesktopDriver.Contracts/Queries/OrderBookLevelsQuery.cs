namespace StockSharp.DesktopDriver.Queries;

using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;

/// <summary>
/// Which levels of an order book to read.
/// </summary>
/// <param name="Side">One side only, or absent for both.</param>
/// <param name="Page">Which page.</param>
/// <param name="Guard">What the caller believes it is reading.</param>
/// <remarks>
/// The ladder is read a page at a time like any other table: a book of a hundred levels each side is an
/// ordinary book, and a reply that carried all of them would be unbounded.
/// </remarks>
public sealed record OrderBookLevelsQuery(
	OrderBookSides? Side,
	UiPageRequest Page,
	UiReadGuard Guard);
