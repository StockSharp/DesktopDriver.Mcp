namespace StockSharp.DesktopDriver.Avalonia;

using System;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia.Threading;

using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// Runs reads on the thread Avalonia keeps its controls on.
/// </summary>
/// <remarks>
/// Avalonia's threading model is the reason this exists at all: a control may only be touched from its
/// dispatcher, and everything else - serialising, waiting, encoding an image - is deliberately kept off
/// it, because whatever occupies that thread stops the application being observed.
/// </remarks>
public sealed class AvaloniaUiExecutor : IUiExecutor
{
	/// <inheritdoc />
	public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

	/// <inheritdoc />
	public Task<T> InvokeAsync<T>(Func<T> read, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(read);

		cancellationToken.ThrowIfCancellationRequested();

		if (Dispatcher.UIThread.CheckAccess())
			return Task.FromResult(read());

		return Dispatcher.UIThread.InvokeAsync(read, DispatcherPriority.Send).GetTask();
	}
}
