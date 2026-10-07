namespace StockSharp.DesktopDriver.Mcp;

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using StockSharp.DesktopDriver.Queries;

/// <summary>
/// Tools that read a document - a code editor, a markdown viewer.
/// </summary>
/// <remarks>
/// Lines are numbered from one, the way an editor's own margin numbers them, so that this and the
/// screen mean the same line.
/// </remarks>
[McpServerToolType]
public static class DocumentTools
{
	[McpServerTool(Name = "ui_document_content", Title = "A document's text", ReadOnly = true)]
	[Description(
		"The lines of a document, numbered from one. ui_snapshot on the same node gives the language, " +
		"the line and character counts, where the caret is, how much is selected and anything the " +
		"editor is complaining about. The text is what the editor is showing, not what was bound into " +
		"it: an editor that was given text and failed to show it reads as empty here, which is the point.")]
	public static Task<string> ContentAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("The document, as scope/identifier.")] string node,
		[Description("Start at this line, counting from one.")] long fromLine = 1,
		[Description("How many lines; 50 by default.")] int limit = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var target = UiAnswers.Node(node);
		var query = new DocumentContentQuery(
			fromLine > 0 ? fromLine : null,
			UiAnswers.Page(limit, null),
			null);

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.ReadDocumentContentAsync(target, query, cancellationToken)));
	}
}
