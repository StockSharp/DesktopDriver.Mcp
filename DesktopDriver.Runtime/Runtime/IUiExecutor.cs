namespace StockSharp.DesktopDriver.Runtime;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Runs short pieces of work where the interface can be touched.
/// </summary>
/// <remarks>
/// Only the reading itself belongs here. Serialising a reply, waiting for a condition and writing an
/// image are done off the interface's thread, because anything that occupies it stops the application
/// the caller is trying to observe.
/// </remarks>
public interface IUiExecutor
{
	/// <summary>
	/// Whether the caller is already on the thread the interface belongs to.
	/// </summary>
	/// <returns><see langword="true"/> when it is.</returns>
	bool CheckAccess();

	/// <summary>
	/// Runs a short read where the interface can be touched.
	/// </summary>
	/// <typeparam name="T">What the read returns.</typeparam>
	/// <param name="read">The read.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>What it returned.</returns>
	Task<T> InvokeAsync<T>(Func<T> read, CancellationToken cancellationToken);
}
