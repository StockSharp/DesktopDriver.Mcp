namespace StockSharp.DesktopDriver.Mcp;

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;

/// <summary>
/// Tools that find out what is on screen.
/// </summary>
/// <remarks>
/// A node is addressed as <c>scope/identifier</c> — the panel, document or window it belongs to, and
/// its identifier inside that. The address survives everything a user can do to the layout, so one
/// found once keeps working after a panel is moved or floated out.
/// <para>
/// Every field that could be missing comes back as a status and a value: either something that was
/// actually read, or the reason there is none. A control with no notion of sorting says so rather than
/// answering "ascending".
/// </para>
/// </remarks>
[McpServerToolType]
public static class SurfaceTools
{
	[McpServerTool(Name = "ui_windows", Title = "Windows on screen", ReadOnly = true)]
	[Description(
		"The windows and popups an application has open, each with the address of its root node. Use " +
		"this to find out where to look before searching inside.")]
	public static Task<string> WindowsAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.GetSurfacesAsync(cancellationToken)));
	}

	[McpServerTool(Name = "ui_find", Title = "Find nodes", ReadOnly = true)]
	[Description(
		"Finds nodes by what they are or what they are called. At least one of the conditions has to be " +
		"given; they all have to match. Answers with addresses to pass to the reading and driving tools. " +
		"Kinds are words such as grid, chart, orderBook, propertyEditor, workspace, button, textBox.")]
	public static Task<string> FindAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("The node's automation identifier, when it has one.")] string automationId = null,
		[Description("What it is called.")] string name = null,
		[Description("What kind of control it is.")] string kind = null,
		[Description("Text it shows.")] string text = null,
		[Description("Only inside this scope.")] string scope = null,
		[Description("Only inside this window or popup.")] string surface = null,
		[Description("How many to answer with; 50 by default.")] int limit = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var selector = new UiSelector(scope, automationId, name, kind, text, surface);

		if (selector.IsEmpty)
		{
			throw new ModelContextProtocol.McpException(
				"ui_find needs something to look for: a name, a kind, an automation identifier, some text, " +
				"a scope or a surface. An empty search would match the first node it passed.");
		}

		var query = new UiFindQuery(selector, UiAnswers.Page(limit, null));

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.FindAsync(query, cancellationToken)));
	}

	[McpServerTool(Name = "ui_tree", Title = "The tree of nodes", ReadOnly = true)]
	[Description(
		"The tree of nodes under one node, or under the whole application when no node is given. Bounded " +
		"by depth and count; the answer says when it was cut short. Reading never changes what it reads: " +
		"it does not open a panel that has never been shown or expand a property that is closed.")]
	public static Task<string> TreeAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("Where to start, as scope/identifier; the whole application when empty.")] string node = null,
		[Description("How deep to go; 4 by default.")] int maxDepth = 0,
		[Description("How many nodes at most; 200 by default.")] int maxNodes = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var id = UiAnswers.NodeId(node);
		var query = new UiTreeQuery(
			id is null ? null : UiTarget.FromId(id),
			false,
			UiCaptureOptions.Default with { Budget = Budget(maxDepth, maxNodes) });

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.GetTreeAsync(query, cancellationToken)));
	}

	[McpServerTool(Name = "ui_snapshot", Title = "One node's state", ReadOnly = true)]
	[Description(
		"Everything one node has to say about itself: whether it is there, enabled, visible and focused, " +
		"where it is, and the state its own kind defines — a grid's counts and sort, a chart's ranges, a " +
		"book's best prices. Use this to check what a click actually did.")]
	public static Task<string> SnapshotAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("Which node, as scope/identifier.")] string node,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var target = UiAnswers.Node(node);

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.CaptureAsync(target, UiCaptureOptions.Default, cancellationToken)));
	}

	private static UiReadBudget Budget(int maxDepth, int maxNodes)
		=> new(
			maxDepth > 0 ? maxDepth : UiReadBudget.Default.MaxDepth,
			maxNodes > 0 ? maxNodes : UiReadBudget.Default.MaxNodes,
			UiReadBudget.Default.MaxItems,
			UiReadBudget.Default.MaxBytes);
}
