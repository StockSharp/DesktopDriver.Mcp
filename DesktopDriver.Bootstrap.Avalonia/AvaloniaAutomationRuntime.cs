namespace StockSharp.DesktopDriver.Bootstrap.Avalonia;

using System;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Avalonia;
using StockSharp.DesktopDriver.Host;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// Everything an application needs while it is being driven, and the way to take it all down again.
/// </summary>
/// <remarks>
/// Held by the application for as long as it runs. Closing it closes the endpoint and unregisters the
/// adapters, so an application that stops offering to be driven really stops.
/// </remarks>
public sealed class AvaloniaAutomationRuntime : IAsyncDisposable
{
	private readonly IDisposable _adapters;

	internal AvaloniaAutomationRuntime(
		UiAutomationService service,
		UiAutomationHost host,
		UiEndpointInfo endpoint,
		AvaloniaNodeBinder binder,
		IDisposable adapters)
	{
		Service = service;
		Host = host;
		Endpoint = endpoint;
		Binder = binder;
		_adapters = adapters;
	}

	/// <summary>
	/// The operations, as a caller inside the process sees them.
	/// </summary>
	public UiAutomationService Service { get; }

	/// <summary>
	/// The endpoint a caller in another process reaches them through.
	/// </summary>
	public UiAutomationHost Host { get; }

	/// <summary>
	/// Where that endpoint is.
	/// </summary>
	public UiEndpointInfo Endpoint { get; }

	/// <summary>
	/// Where controls are given their addresses.
	/// </summary>
	public AvaloniaNodeBinder Binder { get; }

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await Host.DisposeAsync().ConfigureAwait(false);

		_adapters.Dispose();
	}
}
