namespace StockSharp.DesktopDriver.Bootstrap.Maui;

using System;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Host;
using StockSharp.DesktopDriver.Maui;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// Automation running inside a MAUI application, and what stops it.
/// </summary>
public sealed class MauiAutomationRuntime : IAsyncDisposable
{
	private readonly IDisposable _adapters;

	internal MauiAutomationRuntime(
		UiAutomationService service,
		UiAutomationHost host,
		UiEndpointInfo endpoint,
		MauiNodeBinder binder,
		IDisposable adapters)
	{
		Service = service;
		Host = host;
		Endpoint = endpoint;
		Binder = binder;
		_adapters = adapters;
	}

	/// <summary>
	/// What answers the protocol's operations.
	/// </summary>
	public UiAutomationService Service { get; }

	/// <summary>
	/// What listens for runners.
	/// </summary>
	public UiAutomationHost Host { get; }

	/// <summary>
	/// Where runners reach it.
	/// </summary>
	public UiEndpointInfo Endpoint { get; }

	/// <summary>
	/// What turns elements into nodes.
	/// </summary>
	public MauiNodeBinder Binder { get; }

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await Host.DisposeAsync().ConfigureAwait(false);

		_adapters.Dispose();
		Binder.Dispose();
	}
}
