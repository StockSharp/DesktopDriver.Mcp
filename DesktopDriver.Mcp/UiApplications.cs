namespace StockSharp.DesktopDriver.Mcp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol;

using StockSharp.DesktopDriver.Client;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Runner;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// The applications this server is talking to, and the ones it may be asked to start.
/// </summary>
/// <remarks>
/// A caller names an application from the catalogue and never a path of its own. A server that started
/// whatever path it was handed would be a way to run anything on the machine, dressed up as automation.
/// </remarks>
public sealed class UiApplications(UiSettings settings) : IAsyncDisposable
{
	private readonly UiSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
	private readonly Lock _sync = new();
	private readonly Dictionary<string, UiInstance> _instances = new(StringComparer.Ordinal);
	private UiApplicationCatalogue _catalogue;

	/// <summary>
	/// What this server may be asked to start.
	/// </summary>
	/// <returns>The catalogue.</returns>
	/// <exception cref="McpException">There is no catalogue on this machine.</exception>
	public UiApplicationCatalogue Catalogue()
	{
		using (_sync.EnterScope())
		{
			if (_catalogue is not null)
				return _catalogue;
		}

		UiApplicationCatalogue read;

		try
		{
			read = UiApplicationCatalogue.Open(_settings.CataloguePath);
		}
		catch (Exception error) when (error is FileNotFoundException or InvalidOperationException)
		{
			throw new McpException(
				$"This server has no catalogue of applications ({error.Message}). Whoever set it up writes " +
				$"one and names it in {UiSettings.CatalogueVariable}; until then, applications can only be " +
				"attached to after somebody else has started them.");
		}

		using (_sync.EnterScope())
		{
			return _catalogue ??= read;
		}
	}

	/// <summary>
	/// What is running, in the order it was started.
	/// </summary>
	/// <returns>The instances.</returns>
	public UiInstanceInfo[] Running()
	{
		using (_sync.EnterScope())
		{
			return [.. _instances.Values.Select(Describe)];
		}
	}

	/// <summary>
	/// Starts one from the catalogue and waits until it can be driven.
	/// </summary>
	/// <param name="appId">Which application.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The instance.</returns>
	public async Task<UiInstanceInfo> StartAsync(string appId, CancellationToken cancellationToken)
	{
		var entry = Find(appId);

		if (!File.Exists(entry.Executable))
		{
			throw new McpException(
				$"{entry.AppId} has not been built: there is nothing at {entry.Executable}. It has to be built with " +
				"the driver in it, which is usually a separate build from the ordinary one.");
		}

		DrivenApplication started;

		try
		{
			started = await DrivenApplication.StartAsync(
				entry.Executable, entry.Arguments, _settings.StartupPatience, cancellationToken);
		}
		catch (Exception error) when (error is TimeoutException or InvalidOperationException)
		{
			throw new McpException($"{entry.AppId} did not come up: {error.Message}");
		}

		try
		{
			var client = await started.ConnectAsync(entry.AppId, cancellationToken);

			return Remember(entry.AppId, client, started.Endpoint, started);
		}
		catch (Exception)
		{
			await started.DisposeAsync();

			throw;
		}
	}

	/// <summary>
	/// Connects to one somebody else started.
	/// </summary>
	/// <param name="endpointFile">The endpoint file it wrote.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The instance.</returns>
	public async Task<UiInstanceInfo> AttachAsync(
		string endpointFile,
		CancellationToken cancellationToken)
	{
		UiConnection connection;

		try
		{
			connection = UiConnection.To(endpointFile, null, _settings.ConnectPatience);
		}
		catch (Exception error) when (error is UiUnreachableException or ArgumentException)
		{
			throw new McpException(error.Message);
		}

		var client = await Reached(connection.OpenAsync(cancellationToken));

		return Remember(connection.Endpoint.AppId, client, connection.Endpoint, null);
	}

	/// <summary>
	/// Lets go of one, closing it when this server started it.
	/// </summary>
	/// <param name="instance">Which one.</param>
	/// <returns>What was done.</returns>
	public async Task<string> ReleaseAsync(string instance)
	{
		UiInstance found;

		using (_sync.EnterScope())
		{
			if (!_instances.Remove(instance, out found))
				throw Unknown(instance);
		}

		var startedHere = found.WasStartedHere;

		await found.DisposeAsync();

		return startedHere
			? $"{instance} was started by this server and has been closed."
			: $"{instance} was started by somebody else, so it is still running; this server has let go of it.";
	}

	/// <summary>
	/// The open connection to one.
	/// </summary>
	/// <param name="instance">Which one.</param>
	/// <returns>Its client.</returns>
	/// <exception cref="McpException">There is no such instance.</exception>
	public UiAutomationClient Client(string instance)
	{
		using (_sync.EnterScope())
		{
			return _instances.TryGetValue(instance, out var found) ? found.Client : throw Unknown(instance);
		}
	}

	/// <summary>
	/// Runs one operation against an instance, turning a refusal into something the agent can read.
	/// </summary>
	/// <typeparam name="T">What the operation answers.</typeparam>
	/// <param name="instance">Which instance.</param>
	/// <param name="operation">The operation.</param>
	/// <returns>Its answer.</returns>
	public async Task<T> AskAsync<T>(string instance, Func<UiAutomationClient, Task<T>> operation)
	{
		ArgumentNullException.ThrowIfNull(operation);

		return await Reached(operation(Client(instance)));
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		UiInstance[] all;

		using (_sync.EnterScope())
		{
			all = [.. _instances.Values];
			_instances.Clear();
		}

		foreach (var instance in all)
			await instance.DisposeAsync();
	}

	private UiApplicationEntry Find(string appId)
	{
		try
		{
			return Catalogue().Find(appId);
		}
		catch (KeyNotFoundException error)
		{
			throw new McpException(error.Message);
		}
	}

	// Only an McpException carries its message on to the caller; anything else arrives as "an error
	// occurred", which is the one thing a refusal must not say.
	private static async Task<T> Reached<T>(Task<T> operation)
	{
		try
		{
			return await operation;
		}
		catch (UiAutomationException error)
		{
			throw new McpException($"The application refused this ({error.Error.Code}): {error.Error.Message}");
		}
		catch (Exception error) when (error is IOException or TimeoutException or UnauthorizedAccessException)
		{
			throw new McpException($"The application could not be reached: {error.Message}");
		}
	}

	private UiInstanceInfo Remember(
		string appId,
		UiAutomationClient client,
		UiEndpointInfo endpoint,
		DrivenApplication started)
	{
		using (_sync.EnterScope())
		{
			// A second copy of the same product gets a number rather than replacing the first: two are
			// running as often as not, and input sent to the wrong one is a click somebody sees.
			var key = appId;

			for (var ordinal = 2; _instances.ContainsKey(key); ordinal++)
				key = $"{appId}#{ordinal}";

			var instance = new UiInstance(key, client, endpoint, started);

			_instances.Add(key, instance);

			return Describe(instance);
		}
	}

	private static UiInstanceInfo Describe(UiInstance instance)
		=> new(
			instance.Key,
			instance.Client.Session.AppId,
			instance.Endpoint.ProcessId,
			instance.WasStartedHere);

	private static McpException Unknown(string instance)
		=> new($"There is no instance called '{instance}'. ui_list_applications says what is running.");
}
