namespace StockSharp.DesktopDriver.Mcp;

using System;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Client;
using StockSharp.DesktopDriver.Runner;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// One application this server is talking to.
/// </summary>
/// <remarks>
/// Whether this server started it decides what stopping it means. Killing an application somebody else
/// started is not this server's to do, and an agent asked to "close it" would otherwise close a window
/// its owner was working in.
/// </remarks>
public sealed class UiInstance : IAsyncDisposable
{
	private readonly DrivenApplication _started;

	internal UiInstance(string key, UiAutomationClient client, UiEndpointInfo endpoint, DrivenApplication started)
	{
		Key = key;
		Client = client;
		Endpoint = endpoint;
		_started = started;
	}

	/// <summary>
	/// What the tools call it.
	/// </summary>
	public string Key { get; }

	/// <summary>
	/// The open connection.
	/// </summary>
	public UiAutomationClient Client { get; }

	/// <summary>
	/// What the application published about itself.
	/// </summary>
	public UiEndpointInfo Endpoint { get; }

	/// <summary>
	/// Whether this server started the application.
	/// </summary>
	public bool WasStartedHere => _started is not null;

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await Client.DisposeAsync();

		if (_started is not null)
			await _started.DisposeAsync();
	}
}
