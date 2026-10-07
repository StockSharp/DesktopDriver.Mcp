namespace StockSharp.DesktopDriver.Bootstrap.Wpf;

using System;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Host;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Session;
using StockSharp.DesktopDriver.Wpf;

/// <summary>
/// Everything a WPF application needs while it is being driven, and the way to take it all down again.
/// </summary>
/// <remarks>
/// Held by the application for as long as it runs. Closing it closes the endpoint and unregisters the
/// adapters, so an application that stops offering to be driven really stops.
/// </remarks>
public sealed class WpfAutomationRuntime : IAsyncDisposable
{
	private readonly IDisposable _adapters;

	internal WpfAutomationRuntime(
		UiAutomationService service,
		UiAutomationHost host,
		UiEndpointInfo endpoint,
		WpfNodeBinder binder,
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
	public WpfNodeBinder Binder { get; }

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await Host.DisposeAsync().ConfigureAwait(false);

		_adapters.Dispose();
		Binder.Dispose();
	}
}
