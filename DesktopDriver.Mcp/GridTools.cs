namespace StockSharp.DesktopDriver.Mcp;

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.States;

/// <summary>
/// Tools that read a table.
/// </summary>
/// <remarks>
/// A row is named by a key rather than by its position: sorting, filtering and grouping all move rows
/// about, and a test that named the third row would be testing the sort order.
/// <para>
/// A cell carries both the value behind it and the text on screen, and says where each came from. The
/// two differ whenever a column formats what it shows, and which of them a check means is the
/// difference between testing the data and testing the formatting.
/// </para>
/// </remarks>
[McpServerToolType]
public static class GridTools
{
	[McpServerTool(Name = "ui_grid_columns", Title = "A table's columns", ReadOnly = true)]
	[Description(
		"The columns of a table: what each is called, whether it is on show, how wide it is and how it " +
		"is sorted. Read this first — the column identifiers it answers with are what names a cell.")]
	public static Task<string> ColumnsAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("The table, as scope/identifier.")] string node,
		[Description("Only these columns, separated by commas; all of them when empty.")] string columns = null,
		[Description("How many to answer with; 50 by default.")] int limit = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var target = UiAnswers.Node(node);
		var query = new GridColumnsQuery(UiAnswers.List(columns), UiAnswers.Page(limit, null), null);

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.ReadGridColumnsAsync(target, query, cancellationToken)));
	}

	[McpServerTool(Name = "ui_grid_rows", Title = "A table's rows", ReadOnly = true)]
	[Description(
		"The rows of a table, as the table itself has them after sorting, filtering and grouping. " +
		"viewData reads what the table holds; viewport reads only the rows drawn on screen right now. " +
		"Naming rows or columns narrows the answer, which matters: a wide table is a lot of cells.")]
	public static Task<string> RowsAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("The table, as scope/identifier.")] string node,
		[Description("viewData for what the table holds, viewport for what is drawn.")] GridRowsModes mode = GridRowsModes.ViewData,
		[Description("Start at this row number; from the beginning when negative.")] long startIndex = -1,
		[Description("Only these rows, by key, separated by commas.")] string rowKeys = null,
		[Description("Only these columns, separated by commas.")] string columns = null,
		[Description("How many rows to answer with; 50 by default.")] int limit = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var target = UiAnswers.Node(node);
		var query = new GridRowsQuery(
			mode,
			startIndex >= 0 ? startIndex : null,
			UiAnswers.List(rowKeys),
			UiAnswers.List(columns),
			UiAnswers.Page(limit, null),
			null);

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.ReadGridRowsAsync(target, query, cancellationToken)));
	}

	[McpServerTool(Name = "ui_grid_groups", Title = "A table's groups", ReadOnly = true)]
	[Description(
		"The groups a table is grouped into, with the count and the aggregates of each, and whether it " +
		"is open. Naming a group answers with the groups inside it.")]
	public static Task<string> GroupsAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("The table, as scope/identifier.")] string node,
		[Description("Inside this group; the top level when empty.")] string parentGroupId = null,
		[Description("How many to answer with; 50 by default.")] int limit = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var target = UiAnswers.Node(node);
		var query = new GridGroupsQuery(
			string.IsNullOrEmpty(parentGroupId) ? null : parentGroupId,
			UiAnswers.Page(limit, null),
			null);

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.ReadGridGroupsAsync(target, query, cancellationToken)));
	}
}
