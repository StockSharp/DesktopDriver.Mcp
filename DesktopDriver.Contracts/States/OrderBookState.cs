namespace StockSharp.DesktopDriver.States;

using System;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// What an order book is showing.
/// </summary>
/// <param name="InstrumentId">The instrument whose book this is.</param>
/// <param name="ServerTime">When the book was last updated, UTC.</param>
/// <param name="BidCount">How many price levels there are on the buying side.</param>
/// <param name="AskCount">How many on the selling side.</param>
/// <param name="BestBidPrice">The highest price anyone is bidding.</param>
/// <param name="BestBidVolume">How much is bid at it.</param>
/// <param name="BestAskPrice">The lowest price anyone is asking.</param>
/// <param name="BestAskVolume">How much is asked at it.</param>
/// <param name="Spread">The distance between the two.</param>
/// <param name="TotalBidVolume">Everything bid, across all the levels shown.</param>
/// <param name="TotalAskVolume">Everything asked, across all the levels shown.</param>
/// <param name="IsCrossed">Whether the best bid is at or above the best ask.</param>
/// <remarks>
/// The levels themselves are not here. A book is read one page at a time like any other table, because a
/// state that carried every level would be unbounded and one that carried some of them would be a lie
/// about the rest.
/// <para>
/// A crossed book is a real state of the market data and not an error in the reader; it is reported
/// rather than tidied up, because a test about it needs to see it.
/// </para>
/// </remarks>
public sealed record OrderBookState(
	UiField<string> InstrumentId,
	UiField<DateTime> ServerTime,
	UiField<long> BidCount,
	UiField<long> AskCount,
	UiField<decimal> BestBidPrice,
	UiField<decimal> BestBidVolume,
	UiField<decimal> BestAskPrice,
	UiField<decimal> BestAskVolume,
	UiField<decimal> Spread,
	UiField<decimal> TotalBidVolume,
	UiField<decimal> TotalAskVolume,
	bool IsCrossed) : UiState
{
	/// <inheritdoc />
	public override string Kind => UiStateKinds.OrderBook;
}
