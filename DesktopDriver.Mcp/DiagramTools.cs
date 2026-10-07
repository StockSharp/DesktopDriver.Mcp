namespace StockSharp.DesktopDriver.Mcp;

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using StockSharp.DesktopDriver.Queries;

/// <summary>
/// Tools that read a diagram.
/// </summary>
/// <remarks>
/// What the surface drew, not what the composition holds. A block the model has and the surface never
/// presented is exactly the fault a test about a diagram is looking for, and it shows up here as absent.
/// </remarks>
[McpServerToolType]
public static class DiagramTools
{
	[McpServerTool(Name = "ui_diagram_nodes", Title = "A diagram's blocks", ReadOnly = true)]
	[Description(
		"The blocks a diagram drew: what each is, what its caption reads as, whether it is selected and " +
		"where it was drawn. The keys it answers with are what names a block to ui_diagram_connections.")]
	public static Task<string> NodesAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("The diagram, as scope/identifier.")] string node,
		[Description("Only these blocks, by key, separated by commas.")] string keys = null,
		[Description("How many to answer with; 50 by default.")] int limit = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var target = UiAnswers.Node(node);
		var query = new DiagramNodesQuery(UiAnswers.List(keys), UiAnswers.Page(limit, null), null);

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.ReadDiagramNodesAsync(target, query, cancellationToken)));
	}

	[McpServerTool(Name = "ui_diagram_connections", Title = "What a diagram joins", ReadOnly = true)]
	[Description(
		"The connections a diagram routed between its blocks: which socket of which block each leaves " +
		"and reaches, and the polyline it was actually drawn as. Name a block to read only what touches " +
		"it. A connection the composition holds but nothing drew has no route, and says so.")]
	public static Task<string> ConnectionsAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("The diagram, as scope/identifier.")] string node,
		[Description("Only connections touching this block, by its key.")] string nodeKey = null,
		[Description("Only these connections, by key, separated by commas.")] string keys = null,
		[Description("How many to answer with; 50 by default.")] int limit = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var target = UiAnswers.Node(node);
		var query = new DiagramConnectionsQuery(
			string.IsNullOrEmpty(nodeKey) ? null : nodeKey,
			UiAnswers.List(keys),
			UiAnswers.Page(limit, null),
			null);

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.ReadDiagramConnectionsAsync(target, query, cancellationToken)));
	}
}
