namespace StockSharp.DesktopDriver.Mcp;

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using StockSharp.DesktopDriver.Queries;

/// <summary>
/// Tools that read a workspace.
/// </summary>
/// <remarks>
/// The layout is the arrangement of panels, not their contents: which panels there are, which of them
/// is on top of each tab group, whether one is floating and how the space is divided.
/// </remarks>
[McpServerToolType]
public static class DockTools
{
	[McpServerTool(Name = "ui_dock_layout", Title = "A workspace's layout", ReadOnly = true)]
	[Description(
		"How a workspace is arranged: its panels, the tab groups they sit in, which one of each is on " +
		"top, what is floating and how the space is divided. A panel's address from here is what the " +
		"reading tools take.")]
	public static Task<string> LayoutAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("The workspace, as scope/identifier.")] string node,
		[Description("Only this part of the layout; the whole of it when empty.")] string rootLayoutId = null,
		[Description("How deep to go; 4 by default.")] int maxDepth = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var target = UiAnswers.Node(node);
		var budget = maxDepth > 0
			? UiReadBudget.Default with { MaxDepth = maxDepth }
			: UiReadBudget.Default;

		var query = new DockLayoutQuery(
			string.IsNullOrEmpty(rootLayoutId) ? null : rootLayoutId,
			budget,
			null);

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.ReadDockLayoutAsync(target, query, cancellationToken)));
	}
}
