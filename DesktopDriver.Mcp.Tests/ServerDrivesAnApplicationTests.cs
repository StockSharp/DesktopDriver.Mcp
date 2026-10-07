namespace StockSharp.DesktopDriver.Tests.Mcp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ModelContextProtocol.Protocol;

using StockSharp.DesktopDriver.Runner;
using StockSharp.DesktopDriver.Serialization;

/// <summary>
/// The whole way through: an agent starts an application, reads it and takes its picture.
/// </summary>
/// <remarks>
/// Two processes and a real MCP client between them. Everything that could be wrong about this server -
/// the catalogue, the tool names, what the answers actually contain, whether a log line broke stdout -
/// only exists once all three are running.
/// </remarks>
[TestClass]
public class ServerDrivesAnApplicationTests : BaseTestClass
{
	private const string _appId = "desktopdriver.sample";

	[TestMethod]
	[Timeout(180000)]
	public async Task AnAgentAttachesWithOnlyTheEndpointFileAndLeavesTheApplicationRunning()
	{
		await using var app = await DrivenApplication.StartAsync(
			DrivenServer.Executable("SampleExecutable"), null, TimeSpan.FromSeconds(90), CancellationToken);
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);
		Directory.CreateDirectory(server.ArtifactDirectory);
		var endpointFile = Path.Combine(server.ArtifactDirectory, "endpoint.json");
		File.WriteAllText(endpointFile, UiJson.Write(app.Endpoint));

		var attached = await server.CallAsync(
			"ui_attach_application", new Dictionary<string, object> { ["endpointFile"] = endpointFile }, CancellationToken);
		using var document = JsonDocument.Parse(attached);
		var instance = document.RootElement.GetProperty("instance").GetString();
		IsFalse(document.RootElement.GetProperty("wasStartedHere").GetBoolean());

		var windows = await server.CallAsync(
			"ui_windows", new Dictionary<string, object> { ["instance"] = instance }, CancellationToken);
		IsTrue(windows.Contains("window:MainWindow"), windows);

		await ReleaseAsync(server, instance);
		await using var client = await app.ConnectAsync(_appId, CancellationToken);
		IsTrue((await client.GetSurfacesAsync(CancellationToken)).Length > 0);
	}

	[TestMethod]
	public async Task AnAgentStartsAnApplicationAndReadsIt()
	{
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);

		var instance = await StartAsync(server);

		try
		{
			var session = await server.CallAsync(
				"ui_session", new Dictionary<string, object> { ["instance"] = instance }, CancellationToken);

			IsTrue(session.Contains(_appId), session);

			var windows = await server.CallAsync(
				"ui_windows", new Dictionary<string, object> { ["instance"] = instance }, CancellationToken);

			IsTrue(windows.Contains("window:MainWindow"), windows);
		}
		finally
		{
			await ReleaseAsync(server, instance);
		}
	}

	[TestMethod]
	public async Task AnAgentFindsATableAndReadsItsRows()
	{
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);

		var instance = await StartAsync(server);

		try
		{
			var found = await server.CallAsync("ui_find", new Dictionary<string, object>
			{
				["instance"] = instance,
				["kind"] = "grid",
				["limit"] = 5,
			}, CancellationToken);

			var node = FirstNode(found);

			IsNotNull(node, found);

			var columns = await server.CallAsync("ui_grid_columns", new Dictionary<string, object>
			{
				["instance"] = instance,
				["node"] = node,
			}, CancellationToken);

			IsTrue(columns.Contains("\"items\""), columns);

			var rows = await server.CallAsync("ui_grid_rows", new Dictionary<string, object>
			{
				["instance"] = instance,
				["node"] = node,
				["limit"] = 2,
			}, CancellationToken);

			// Two rows asked for out of a table that holds more: the answer has to say it was cut short
			// rather than look complete.
			IsTrue(rows.Contains("\"truncated\":true"), rows);
		}
		finally
		{
			await ReleaseAsync(server, instance);
		}
	}

	[TestMethod]
	public async Task AnAddressThatIsNotAnAddressIsRefusedInWords()
	{
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);

		var instance = await StartAsync(server);

		try
		{
			var refusal = await server.RefusalAsync("ui_snapshot", new Dictionary<string, object>
			{
				["instance"] = instance,
				["node"] = "OrderGrid",
			}, CancellationToken);

			IsNotNull(refusal, "The server accepted something that is not an address.");
			IsTrue(refusal.Contains("scope/identifier"), refusal);
		}
		finally
		{
			await ReleaseAsync(server, instance);
		}
	}

	[TestMethod]
	public async Task LookingForNothingInParticularIsRefused()
	{
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);

		var instance = await StartAsync(server);

		try
		{
			var refusal = await server.RefusalAsync(
				"ui_find", new Dictionary<string, object> { ["instance"] = instance }, CancellationToken);

			IsNotNull(refusal, "An empty search was accepted; it would match the first node it passed.");
		}
		finally
		{
			await ReleaseAsync(server, instance);
		}
	}

	[TestMethod]
	public async Task APictureIsWrittenToAFileAndTheAgentIsToldWhere()
	{
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);

		var instance = await StartAsync(server);

		try
		{
			var taken = await server.CallAsync("ui_screenshot", new Dictionary<string, object>
			{
				["instance"] = instance,
				["node"] = "window:MainWindow/~root",
			}, CancellationToken);

			var path = JsonDocument.Parse(taken).RootElement.GetProperty("path").GetString();

			IsTrue(File.Exists(path), $"{taken} names a file that is not there.");
			IsTrue(new FileInfo(path).Length > 0, path);
		}
		finally
		{
			await ReleaseAsync(server, instance);
		}
	}

	[TestMethod]
	public async Task APictureCanAlsoComeBackAsAnImage()
	{
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);

		var instance = await StartAsync(server);

		try
		{
			var node = FirstNode(await server.CallAsync("ui_find", new Dictionary<string, object>
			{
				["instance"] = instance,
				["kind"] = "grid",
				["limit"] = 1,
			}, CancellationToken));

			var content = await server.CallForContentAsync("ui_screenshot_image", new Dictionary<string, object>
			{
				["instance"] = instance,
				["node"] = node,
			}, CancellationToken);

			var image = content.OfType<ImageContentBlock>().FirstOrDefault();

			IsNotNull(image, $"Nothing came back as an image: {string.Join(" | ", content.Select(block => block is TextContentBlock text ? text.Text : block.Type))}");
			AreEqual("image/png", image.MimeType);
			IsTrue(image.Data.Length > 0, "The image is empty.");
		}
		finally
		{
			await ReleaseAsync(server, instance);
		}
	}

	[TestMethod]
	public async Task LettingGoOfAnApplicationThisServerStartedClosesIt()
	{
		await using var server = await DrivenServer.StartAsync(Catalogue(), CancellationToken);

		var instance = await StartAsync(server);
		var said = await ReleaseAsync(server, instance);

		IsTrue(said.Contains("closed"), said);

		// And it is gone: reading it now is a refusal about an instance nobody is talking to.
		var refusal = await server.RefusalAsync(
			"ui_windows", new Dictionary<string, object> { ["instance"] = instance }, CancellationToken);

		IsNotNull(refusal, "The server is still talking to an application it closed.");
	}

	private async Task<string> StartAsync(DrivenServer server)
	{
		var started = await server.CallAsync(
			"ui_start_application", new Dictionary<string, object> { ["appId"] = _appId }, CancellationToken);

		var instance = JsonDocument.Parse(started).RootElement.GetProperty("instance").GetString();

		IsFalse(string.IsNullOrEmpty(instance), started);

		return instance;
	}

	private Task<string> ReleaseAsync(DrivenServer server, string instance)
		=> server.CallAsync(
			"ui_release_application", new Dictionary<string, object> { ["instance"] = instance }, CancellationToken);

	private static Dictionary<string, string> Catalogue()
		=> new() { [_appId] = DrivenServer.Executable("SampleExecutable") };

	private static string FirstNode(string found)
	{
		var items = JsonDocument.Parse(found).RootElement.GetProperty("items");

		if (items.GetArrayLength() == 0)
			return null;

		var id = items[0].GetProperty("id");

		return $"{id.GetProperty("scopeId").GetString()}/{id.GetProperty("localId").GetString()}";
	}
}
