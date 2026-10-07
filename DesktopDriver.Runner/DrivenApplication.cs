namespace StockSharp.DesktopDriver.Runner;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Client;
using StockSharp.DesktopDriver.Protocol;
using StockSharp.DesktopDriver.Serialization;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// A product, started the way a runner starts it, and talked to the way a runner talks to it.
/// </summary>
/// <remarks>
/// Its own process with its own window: this is the only kind of test that can tell whether the
/// integration works, because the switch, the endpoint and the window tracker only exist in a real start-up.
/// </remarks>
public sealed class DrivenApplication : IAsyncDisposable
{
	private readonly Process _process;
	private readonly string _endpointFile;

	private DrivenApplication(Process process, string endpointFile, UiEndpointInfo endpoint)
	{
		_process = process;
		_endpointFile = endpointFile;
		Endpoint = endpoint;
	}

	/// <summary>
	/// Where it is listening.
	/// </summary>
	public UiEndpointInfo Endpoint { get; }

	/// <summary>
	/// The path of an executable a test assembly named at build time.
	/// </summary>
	/// <param name="assembly">The test assembly that names it.</param>
	/// <param name="key">Which one.</param>
	/// <returns>Its path.</returns>
	/// <remarks>
	/// The assembly is named rather than assumed: this helper lives in a library shared by several test
	/// projects, and the build-time path belongs to whichever of them is asking.
	/// </remarks>
	public static string Executable(Assembly assembly, string key)
	{
		ArgumentNullException.ThrowIfNull(assembly);

		var found = assembly
			.GetCustomAttributes<AssemblyMetadataAttribute>()
			.FirstOrDefault(attribute => attribute.Key == key)
			?? throw new InvalidOperationException($"{assembly.GetName().Name} does not say where {key} is.");

		return found.Value;
	}

	/// <summary>
	/// Starts one and waits until its endpoint is open.
	/// </summary>
	/// <param name="executable">The product's executable.</param>
	/// <param name="extraArguments">Anything the product itself needs.</param>
	/// <param name="patience">How long to wait for it to come up.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The running application.</returns>
	public static async Task<DrivenApplication> StartAsync(
		string executable,
		string extraArguments,
		TimeSpan patience,
		CancellationToken cancellationToken)
	{
		if (!File.Exists(executable))
		{
			throw new FileNotFoundException(
				$"{executable} has not been built. The test project builds it; build the test project.",
				executable);
		}

		var endpointFile = Path.Combine(
			Path.GetDirectoryName(executable),
			$"endpoint.{Guid.NewGuid():N}.json");

		var start = new ProcessStartInfo(executable)
		{
			UseShellExecute = false,
			RedirectStandardError = true,
			WorkingDirectory = Path.GetDirectoryName(executable),
		};

		foreach (var argument in Arguments(extraArguments, endpointFile))
			start.ArgumentList.Add(argument);

		var process = Process.Start(start);

		try
		{
			var endpoint = await WaitForEndpointAsync(process, endpointFile, patience, cancellationToken);

			return new DrivenApplication(process, endpointFile, endpoint);
		}
		catch (Exception)
		{
			Kill(process);
			Delete(endpointFile);

			throw;
		}
	}

	/// <summary>
	/// Connects to it the way a runner does.
	/// </summary>
	/// <param name="expectedAppId">The product the caller expects to reach.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The connected client.</returns>
	public Task<UiAutomationClient> ConnectAsync(string expectedAppId, CancellationToken cancellationToken)
		=> UiAutomationClient.ConnectAsync(
			Endpoint, expectedAppId, TimeSpan.FromSeconds(15), cancellationToken);

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		Kill(_process);
		Delete(_endpointFile);

		return ValueTask.CompletedTask;
	}

	private static string[] Arguments(string extraArguments, string endpointFile)
	{
		var arguments = string.IsNullOrEmpty(extraArguments)
			? Array.Empty<string>()
			: extraArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);

		return
		[
			.. arguments,
			UiLaunchProtocol.EnableSwitch,
			UiLaunchProtocol.EndpointSwitch + endpointFile,
		];
	}

	private static async Task<UiEndpointInfo> WaitForEndpointAsync(
		Process process,
		string endpointFile,
		TimeSpan patience,
		CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + patience;

		while (DateTime.UtcNow < deadline)
		{
			if (process.HasExited)
			{
				var error = await process.StandardError.ReadToEndAsync(cancellationToken);

				throw new InvalidOperationException(
					$"The application stopped with code {process.ExitCode} before opening its endpoint. {error}");
			}

			if (UiEndpointFile.TryRead(endpointFile, out var endpoint))
				return endpoint;

			await Task.Delay(100, cancellationToken);
		}

		throw new TimeoutException($"The application did not open its endpoint within {patience}.");
	}

	private static void Kill(Process process)
	{
		try
		{
			if (process is not null && !process.HasExited)
				process.Kill(true);
		}
		catch (Exception)
		{
			// A process that is already gone is the outcome that was wanted.
		}
	}

	private static void Delete(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch (Exception)
		{
			// Leaving one behind is untidy, not wrong.
		}
	}
}
