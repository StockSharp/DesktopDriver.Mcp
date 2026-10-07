namespace StockSharp.DesktopDriver.Mcp;

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.States;

/// <summary>
/// Tools that read an order book.
/// </summary>
/// <remarks>
/// A level is named by its side and its price, because that is what a level is. Its position in the
/// book changes with every quote that arrives, and the price does not.
/// </remarks>
[McpServerToolType]
public static class OrderBookTools
{
	[McpServerTool(Name = "ui_order_book_levels", Title = "An order book's levels", ReadOnly = true)]
	[Description(
		"The levels an order book is showing, keyed by side and price, as bid@100.5 or ask@100.7. " +
		"Naming a side answers with only that half. ui_snapshot on the same node gives the best prices, " +
		"the spread, the totals per side and whether the book is crossed.")]
	public static Task<string> LevelsAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("The order book, as scope/identifier.")] string node,
		[Description("Only one side: Bid or Ask; both when not given.")] OrderBookSides? side = null,
		[Description("How many to answer with; 50 by default.")] int limit = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var target = UiAnswers.Node(node);
		var query = new OrderBookLevelsQuery(side, UiAnswers.Page(limit, null), null);

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.ReadOrderBookLevelsAsync(target, query, cancellationToken)));
	}
}
