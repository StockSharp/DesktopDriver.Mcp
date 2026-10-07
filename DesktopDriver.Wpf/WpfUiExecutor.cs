namespace StockSharp.DesktopDriver.Wpf;

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// Runs reads on the thread WPF keeps its controls on.
/// </summary>
/// <remarks>
/// WPF's threading model is the reason this exists at all: a control may only be touched from its
/// dispatcher, and everything else - serialising, waiting, encoding an image - is deliberately kept off
/// it, because whatever occupies that thread stops the application being observed.
/// </remarks>
public sealed class WpfUiExecutor : IUiExecutor
{
	/// <summary>
	/// The dispatcher the running application's controls belong to.
	/// </summary>
	/// <remarks>
	/// Asked for each time rather than captured once: this executor is built while the application is
	/// still starting, before there is an application to ask.
	/// </remarks>
	private static Dispatcher UiThread
		=> Application.Current?.Dispatcher ?? throw new InvalidOperationException("There is no running WPF application to read from.");

	/// <inheritdoc />
	public bool CheckAccess() => UiThread.CheckAccess();

	/// <inheritdoc />
	public Task<T> InvokeAsync<T>(Func<T> read, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(read);

		cancellationToken.ThrowIfCancellationRequested();

		var uiThread = UiThread;

		if (uiThread.CheckAccess())
			return Task.FromResult(read());

		return uiThread.InvokeAsync(read, DispatcherPriority.Send, cancellationToken).Task;
	}
}
