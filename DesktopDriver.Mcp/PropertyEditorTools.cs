namespace StockSharp.DesktopDriver.Mcp;

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

using StockSharp.DesktopDriver.Queries;

/// <summary>
/// Tools that read a property editor.
/// </summary>
/// <remarks>
/// What comes back is what the editor is showing — after its categories, its basic mode and its search
/// box — and not what the object behind it holds. A read that went to the object instead would see none
/// of those, and would pass with the editor showing nothing at all.
/// </remarks>
[McpServerToolType]
public static class PropertyEditorTools
{
	[McpServerTool(Name = "ui_property_items", Title = "A property editor's properties", ReadOnly = true)]
	[Description(
		"The properties a property editor is showing, each by its path on the object, with its value, " +
		"the text on screen, what kind of editor it calls for, and anything the editor is refusing. A " +
		"property nobody has opened has no properties of its own yet: opening one is something a person " +
		"does, and a read will not do it.")]
	public static Task<string> ItemsAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("The property editor, as scope/identifier.")] string node,
		[Description("Only these properties, by path, separated by commas.")] string paths = null,
		[Description("How many to answer with; 50 by default.")] int limit = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var target = UiAnswers.Node(node);
		var query = new PropertyEditorItemsQuery(UiAnswers.List(paths), UiAnswers.Page(limit, null), null);

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.ReadPropertyEditorItemsAsync(target, query, cancellationToken)));
	}
}
