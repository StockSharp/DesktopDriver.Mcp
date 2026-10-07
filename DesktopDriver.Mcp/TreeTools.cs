namespace StockSharp.DesktopDriver.Mcp;

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using StockSharp.DesktopDriver.Queries;

/// <summary>
/// Tools that read a tree.
/// </summary>
/// <remarks>
/// An item is named by its path through the tree - "Strategies/Sma/Errors" - rather than by a position,
/// because a position changes with every item opened above it. Two siblings that read the same get a
/// number.
/// </remarks>
[McpServerToolType]
public static class TreeTools
{
	[McpServerTool(Name = "ui_tree_items", Title = "A tree's items", ReadOnly = true)]
	[Description(
		"The items a tree is showing, each by its path, with its text and whether it is open and " +
		"selected. An item nobody has opened has nothing inside it yet: opening one is something a " +
		"person does, and a read will not do it. Name a parent to read only what is directly inside it.")]
	public static Task<string> ItemsAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("The tree, as scope/identifier.")] string node,
		[Description("Only what is directly inside this item, by its path.")] string parentKey = null,
		[Description("Only these items, by path, separated by commas.")] string keys = null,
		[Description("How many to answer with; 50 by default.")] int limit = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var target = UiAnswers.Node(node);
		var query = new TreeItemsQuery(
			string.IsNullOrEmpty(parentKey) ? null : parentKey,
			UiAnswers.List(keys),
			UiAnswers.Page(limit, null),
			null);

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.ReadTreeItemsAsync(target, query, cancellationToken)));
	}
}
