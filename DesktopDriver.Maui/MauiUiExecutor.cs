namespace StockSharp.DesktopDriver.Maui;

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;

using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// Runs reads and actions on the thread that owns the application's interface.
/// </summary>
public sealed class MauiUiExecutor : IUiExecutor
{
	private static IDispatcher UiThread
		=> Application.Current?.Dispatcher ?? throw new InvalidOperationException("There is no running MAUI application to read from.");

	/// <inheritdoc />
	public bool CheckAccess() => !UiThread.IsDispatchRequired;

	/// <inheritdoc />
	public Task<T> InvokeAsync<T>(Func<T> read, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(read);

		cancellationToken.ThrowIfCancellationRequested();

		var uiThread = UiThread;

		if (!uiThread.IsDispatchRequired)
			return Task.FromResult(read());

		// The queue takes no token, so a read cancelled while it waited is refused when its turn comes.
		return uiThread.DispatchAsync(() =>
		{
			cancellationToken.ThrowIfCancellationRequested();

			return read();
		});
	}

	/// <summary>
	/// Runs work that has to wait for the interface on the thread that owns it - a picture is drawn by the
	/// interface in its own time, and the thread stays free while it is.
	/// </summary>
	/// <typeparam name="T">What the work answers.</typeparam>
	/// <param name="work">The work.</param>
	/// <param name="cancellationToken">Stops the work before it starts.</param>
	/// <returns>What the work answered.</returns>
	public Task<T> InvokeAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(work);

		cancellationToken.ThrowIfCancellationRequested();

		var uiThread = UiThread;

		if (!uiThread.IsDispatchRequired)
			return work();

		return uiThread.DispatchAsync(() =>
		{
			cancellationToken.ThrowIfCancellationRequested();

			return work();
		});
	}
}
