namespace StockSharp.DesktopDriver.Mcp;

using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Entry point of the MCP server that reads and drives the running Avalonia applications.
/// </summary>
public static class Program
{
	/// <summary>
	/// Runs the server until its client closes stdin.
	/// </summary>
	/// <param name="args">Command line, unused; everything is configured through the environment.</param>
	/// <returns>Completes when the server stops.</returns>
	public static async Task Main(string[] args)
	{
		var builder = Host.CreateApplicationBuilder(args);

		// stdout carries the protocol, so every log line has to go to stderr instead.
		builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

		builder.Services.AddSingleton<UiSettings>();
		builder.Services.AddSingleton<UiApplications>();
		builder.Services
			.AddMcpServer(options => options.ServerInfo = new()
			{
				Name = "StockSharp.DesktopDriver",
				Title = "StockSharp DesktopDriver",
				Version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
					?? typeof(Program).Assembly.GetName().Version?.ToString(),
			})
			.WithStdioServerTransport()
			.WithToolsFromAssembly();

		await builder.Build().RunAsync();
	}
}
