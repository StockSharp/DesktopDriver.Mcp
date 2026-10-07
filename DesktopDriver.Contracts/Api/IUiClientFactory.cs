namespace StockSharp.DesktopDriver.Api;

using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Session;

/// <summary>
/// Opens connections to running applications.
/// </summary>
public interface IUiClientFactory
{
	/// <summary>
	/// Connects.
	/// </summary>
	/// <param name="options">Which application, and how long to wait for it.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The connected client.</returns>
	Task<IUiAutomationClient> ConnectAsync(
		UiConnectOptions options,
		CancellationToken cancellationToken);
}
