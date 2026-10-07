namespace StockSharp.DesktopDriver.Api;

using System;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Session;

/// <summary>
/// The endpoint an application opens so that it can be driven.
/// </summary>
public interface IUiAutomationHost : IAsyncDisposable
{
	/// <summary>
	/// Starts listening.
	/// </summary>
	/// <param name="options">How.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>Where it is listening.</returns>
	Task<UiEndpointInfo> StartAsync(UiHostOptions options, CancellationToken cancellationToken);

	/// <summary>
	/// Stops listening and releases what it held.
	/// </summary>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>A task that completes when it has stopped.</returns>
	Task StopAsync(CancellationToken cancellationToken);
}
