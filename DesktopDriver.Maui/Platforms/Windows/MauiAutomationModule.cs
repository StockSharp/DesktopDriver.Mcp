namespace StockSharp.DesktopDriver.Maui;

using System;

using Microsoft.Maui.Controls;

using StockSharp.DesktopDriver.Adapters;

/// <summary>
/// The adapters every MAUI application is read with.
/// </summary>
/// <param name="binder">Registers the elements the adapters report.</param>
public sealed class MauiAutomationModule(MauiNodeBinder binder) : IUiAutomationModule
{
	private readonly MauiNodeBinder _binder = binder ?? throw new ArgumentNullException(nameof(binder));

	/// <inheritdoc />
	public string ModuleId => "maui.standard";

	/// <inheritdoc />
	public IDisposable Register(IUiAdapterRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);

		var adapters = new[]
		{
			// The fallback is registered for Element rather than View: a window, a page and an entry of a shell
			// are no views, and a fallback that missed them would leave the frame of every screen unreadable.
			registry.Register(
				"maui.control",
				typeof(Element),
				new MauiControlAdapter(_binder),
				priority: int.MinValue),
			// A list, so that a row of it can be pointed at: its rows are values rather than controls, and
			// there is nothing else to name one by.
			registry.Register(
				"maui.list",
				typeof(ItemsView),
				new MauiListAdapter(_binder),
				priority: 10),
			// An entry of a shell is drawn by the platform - a line of the flyout, a tab - and has no control
			// of its own to point at.
			registry.Register(
				"maui.shellItem",
				typeof(BaseShellItem),
				new MauiShellItemAdapter(_binder),
				priority: 10),
		};

		return new StandardRegistration(adapters);
	}

	private sealed class StandardRegistration(IDisposable[] adapters) : IDisposable
	{
		public void Dispose()
		{
			foreach (var adapter in adapters)
				adapter.Dispose();
		}
	}
}
