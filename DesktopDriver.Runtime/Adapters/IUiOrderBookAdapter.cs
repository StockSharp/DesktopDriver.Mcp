namespace StockSharp.DesktopDriver.Adapters;

using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.States;

/// <summary>
/// Reads the price levels of an order book.
/// </summary>
/// <remarks>
/// A book is drawn as a table, so its rows are readable as rows like any other table's. This is the other
/// question about it: a level is a price and a volume on a side, and a caller that had to work that out of
/// cells would be re-implementing the book - differently in every test.
/// </remarks>
public interface IUiOrderBookAdapter : IUiSnapshotAdapter
{
	/// <summary>
	/// Reads the levels, in the order the book is showing them.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="query">Which levels.</param>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>One page of levels.</returns>
	UiDataPage<OrderBookLevelSnapshot> ReadLevels(
		UiSubject subject,
		OrderBookLevelsQuery query,
		UiCaptureContext context);
}
