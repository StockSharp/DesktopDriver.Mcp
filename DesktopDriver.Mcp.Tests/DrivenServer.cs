namespace StockSharp.DesktopDriver.Tests.Mcp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

/// <summary>
/// The MCP server, spoken to the way an agent speaks to it.
/// </summary>
/// <remarks>
/// Its own process and a real client over stdio. A test that called the tool methods in-process would
/// never see the tool names, the schemas, or a log line that went to stdout and broke the protocol.
/// </remarks>
public sealed class DrivenServer : IAsyncDisposable
{
	private readonly McpClient _client;
	private readonly string _home;

	private DrivenServer(McpClient client, string home)
	{
		_client = client;
		_home = home;
	}

	/// <summary>
	/// What the server offers.
	/// </summary>
	public IList<McpClientTool> Tools { get; private set; }

	/// <summary>
	/// Where this server writes its pictures.
	/// </summary>
	public string ArtifactDirectory => Path.Combine(_home, "artifacts");

	/// <summary>
	/// Starts one with a catalogue naming the given applications.
	/// </summary>
	/// <param name="catalogue">What the catalogue lists, as appId to executable.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The connected server.</returns>
	public static async Task<DrivenServer> StartAsync(
		IReadOnlyDictionary<string, string> catalogue,
		CancellationToken cancellationToken)
	{
		var executable = Executable("McpServerExecutable");

		if (!File.Exists(executable))
		{
			throw new FileNotFoundException(
				$"{executable} has not been built. The test project builds it; build the test project.",
				executable);
		}

		// Its own directory per test, so what one test starts and writes cannot be seen by the next. Kept
		// under this test's own output rather than in the machine's temp folder, where it would outlive
		// the repository it belongs to.
		var home = Path.Combine(AppContext.BaseDirectory, "mcp-runs", $"{Guid.NewGuid():N}");

		Directory.CreateDirectory(home);

		var cataloguePath = Path.Combine(home, "applications.json");

		File.WriteAllText(cataloguePath, JsonSerializer.Serialize(new
		{
			applications = catalogue.Select(pair => new
			{
				appId = pair.Key,
				executable = pair.Value,
				arguments = string.Empty,
				description = $"{pair.Key}, as this test set it up.",
			}),
		}));

		var transport = new StdioClientTransport(new StdioClientTransportOptions
		{
			Name = "desktop-driver",
			Command = executable,
			EnvironmentVariables = new Dictionary<string, string>
			{
				["STOCKSHARP_UI_CATALOGUE"] = cataloguePath,
				["STOCKSHARP_UI_ARTIFACTS"] = Path.Combine(home, "artifacts"),
			},
		});

		var client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
		var server = new DrivenServer(client, home)
		{
			Tools = await client.ListToolsAsync(cancellationToken: cancellationToken),
		};

		return server;
	}

	/// <summary>
	/// The path a test assembly named at build time.
	/// </summary>
	/// <param name="key">Which one.</param>
	/// <returns>Its path.</returns>
	public static string Executable(string key)
	{
		var found = typeof(DrivenServer).Assembly
			.GetCustomAttributes<AssemblyMetadataAttribute>()
			.FirstOrDefault(attribute => attribute.Key == key)
			?? throw new InvalidOperationException($"This test assembly does not say where {key} is.");

		return found.Value;
	}

	/// <summary>
	/// Calls a tool and answers with the text it produced.
	/// </summary>
	/// <param name="tool">Which tool.</param>
	/// <param name="arguments">Its arguments.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>What it said.</returns>
	public async Task<string> CallAsync(
		string tool,
		IReadOnlyDictionary<string, object> arguments,
		CancellationToken cancellationToken)
	{
		var result = await _client.CallToolAsync(tool, arguments, cancellationToken: cancellationToken);

		if (result.IsError == true)
			throw new InvalidOperationException($"{tool} refused: {Text(result)}");

		return Text(result);
	}

	/// <summary>
	/// Calls a tool expecting it to refuse, and answers with what it said.
	/// </summary>
	/// <param name="tool">Which tool.</param>
	/// <param name="arguments">Its arguments.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>What it said, or <see langword="null"/> when it did not refuse.</returns>
	public async Task<string> RefusalAsync(
		string tool,
		IReadOnlyDictionary<string, object> arguments,
		CancellationToken cancellationToken)
	{
		try
		{
			var result = await _client.CallToolAsync(tool, arguments, cancellationToken: cancellationToken);

			return result.IsError == true ? Text(result) : null;
		}
		catch (ModelContextProtocol.McpException error)
		{
			return error.Message;
		}
	}

	/// <summary>
	/// Calls a tool and answers with everything it produced.
	/// </summary>
	/// <param name="tool">Which tool.</param>
	/// <param name="arguments">Its arguments.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>Its content.</returns>
	public async Task<IList<ContentBlock>> CallForContentAsync(
		string tool,
		IReadOnlyDictionary<string, object> arguments,
		CancellationToken cancellationToken)
	{
		var result = await _client.CallToolAsync(tool, arguments, cancellationToken: cancellationToken);

		return result.Content;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await _client.DisposeAsync();

		try
		{
			Directory.Delete(_home, recursive: true);
		}
		catch (IOException)
		{
			// Leaving one behind is untidy, not wrong.
		}
	}

	private static string Text(CallToolResult result)
		=> string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
