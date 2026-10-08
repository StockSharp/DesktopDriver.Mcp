namespace StockSharp.DesktopDriver.Tests.Mcp;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// What the server offers an agent before anything is running.
/// </summary>
[TestClass]
public class ServerSurfaceTests : BaseTestClass
{
	private static readonly string[] _expected =
	[
		"ui_list_applications", "ui_start_application", "ui_attach_application",
		"ui_release_application", "ui_session",
		"ui_windows", "ui_find", "ui_tree", "ui_snapshot",
		"ui_grid_columns", "ui_grid_rows", "ui_grid_groups",
		"ui_chart_series", "ui_chart_points",
		"ui_order_book_levels", "ui_property_items", "ui_dock_layout",
		"ui_tree_items", "ui_document_content", "ui_diagram_nodes", "ui_diagram_connections",
		"ui_click", "ui_type_text", "ui_press_key", "ui_scroll", "ui_bring_into_view",
		"ui_action_status", "ui_wait",
		"ui_screenshot", "ui_screenshot_image",
		"ui_diagnostics",
	];

	/// <summary>The connected server reports the product identity and display name.</summary>
	[TestMethod]
	[Timeout(60000)]
	public async Task ServerReportsItsStockSharpIdentity()
	{
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);

		AreEqual("StockSharp.DesktopDriver", server.ServerInfo.Name);
		AreEqual("StockSharp DesktopDriver", server.ServerInfo.Title);
		IsFalse(string.IsNullOrWhiteSpace(server.ServerInfo.Version));
	}

	[TestMethod]
	public async Task EveryToolTheAgentNeedsIsThere()
	{
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);

		var offered = server.Tools.Select(tool => tool.Name).ToHashSet();

		foreach (var name in _expected)
			IsTrue(offered.Contains(name), $"{name} is not offered; the server offers {string.Join(", ", offered.Order())}.");
	}

	[TestMethod]
	public async Task NothingIsOfferedThatNobodyWrote()
	{
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);

		// A tool that appeared without being listed here is one nobody described to the agent either.
		foreach (var tool in server.Tools)
			IsTrue(_expected.Contains(tool.Name), $"{tool.Name} is offered but not accounted for.");
	}

	[TestMethod]
	public async Task EveryToolSaysWhatItIsFor()
	{
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);

		foreach (var tool in server.Tools)
		{
			IsFalse(string.IsNullOrWhiteSpace(tool.Description), $"{tool.Name} has no description.");

			// Long enough to say what the tool does and what it does not: an agent chooses from these
			// alone, and a one-liner is how the wrong tool gets picked.
			IsTrue(tool.Description.Length > 60, $"{tool.Name} says too little: {tool.Description}");
		}
	}

	[TestMethod]
	[Timeout(60000)]
	public async Task AttachingNeedsOnlyTheEndpointFile()
	{
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);
		var tool = server.Tools.Single(item => item.Name == "ui_attach_application");
		var properties = tool.ProtocolTool.InputSchema.GetProperty("properties");

		AreEqual(1, properties.EnumerateObject().Count());
		IsTrue(properties.TryGetProperty("endpointFile", out _));
	}

	[TestMethod]
	public async Task ReadingIsMarkedAsReading()
	{
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);

		foreach (var name in _expected.Where(item => item.Contains("_grid_") || item.Contains("_chart_")))
		{
			var tool = server.Tools.First(item => item.Name == name);

			// A client that shows its user which tools only look is reading this, and a read marked as a
			// change gets a confirmation prompt it never needed.
			IsTrue(
				tool.ProtocolTool.Annotations?.ReadOnlyHint == true,
				$"{name} does not say it only reads.");
		}
	}

	[TestMethod]
	public async Task DrivingIsNotMarkedAsReading()
	{
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);

		foreach (var name in new[] { "ui_click", "ui_type_text", "ui_press_key", "ui_scroll" })
		{
			var tool = server.Tools.First(item => item.Name == name);

			IsFalse(
				tool.ProtocolTool.Annotations?.ReadOnlyHint == true,
				$"{name} says it only reads, and it clicks.");
		}
	}

	[TestMethod]
	public async Task AnApplicationNobodyListedCannotBeStarted()
	{
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);

		var refusal = await server.RefusalAsync(
			"ui_start_application",
			new Dictionary<string, object> { ["appId"] = "C:\\Windows\\System32\\cmd.exe" },
			CancellationToken);

		// Naming a path instead of an application is how a server that started what it was handed would
		// become a way to run anything on the machine.
		IsNotNull(refusal, "The server did not refuse to start something that is not in its catalogue.");
		IsTrue(refusal.Contains("catalogue"), refusal);
	}

	[TestMethod]
	public async Task AnInstanceNobodyStartedCannotBeRead()
	{
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);

		var refusal = await server.RefusalAsync(
			"ui_windows",
			new Dictionary<string, object> { ["instance"] = "stocksharp.terminal" },
			CancellationToken);

		IsNotNull(refusal, "The server answered about an application it is not talking to.");
		IsTrue(refusal.Contains("ui_list_applications"), refusal);
	}

	[TestMethod]
	public async Task TheCatalogueSaysWhatCanBeStartedAndWhatIsBuilt()
	{
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);

		var listed = await server.CallAsync("ui_list_applications", new Dictionary<string, object>(), CancellationToken);

		IsTrue(listed.Contains("desktopdriver.sample"), listed);
		IsTrue(listed.Contains("\"isBuilt\":true") || listed.Contains("\"IsBuilt\":true"), listed);
	}

	private static Dictionary<string, string> Catalogue()
		=> new() { ["desktopdriver.sample"] = DrivenServer.Executable("SampleExecutable") };
}
