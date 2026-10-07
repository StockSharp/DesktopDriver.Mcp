namespace StockSharp.DesktopDriver.Mcp;

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using StockSharp.DesktopDriver.Diagnostics;

/// <summary>
/// Tools that read what the module itself has been saying.
/// </summary>
[McpServerToolType]
public static class DiagnosticTools
{
	[McpServerTool(Name = "ui_diagnostics", Title = "What the module has been saying", ReadOnly = true)]
	[Description(
		"What the automation module itself has recorded — refusals, things it could not read, input it " +
		"could not deliver — newest last, with a cursor to carry on from. Worth reading when a tool " +
		"answered something surprising and the reason is not in the answer.")]
	public static Task<string> DiagnosticsAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("Carry on from this cursor; from the beginning when empty.")] string afterCursor = null,
		[Description("How many to answer with; 100 by default.")] int limit = 0,
		[Description("Only entries about this node, as scope/identifier.")] string node = null,
		[Description("Only entries about this action.")] string actionId = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var query = new UiDiagnosticQuery(
			string.IsNullOrEmpty(afterCursor) ? null : afterCursor,
			limit > 0 ? limit : 100,
			UiAnswers.NodeId(node),
			string.IsNullOrEmpty(actionId) ? null : UiAnswers.Identifier(actionId, "an action"));

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.ReadDiagnosticsAsync(query, cancellationToken)));
	}
}
