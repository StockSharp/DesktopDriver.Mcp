namespace StockSharp.DesktopDriver.Wpf;

using System;
using System.Windows;
using System.Windows.Controls;

using StockSharp.DesktopDriver.Adapters;

/// <summary>
/// Registers what can be read out of an ordinary WPF control.
/// </summary>
/// <remarks>
/// The fallback, at the lowest priority, covers every control; anything written for a particular control
/// - a grid, a chart, a dock - is registered by its own module and wins over it.
/// <para>
/// A tree is registered here rather than in a product module because a tree is an ordinary WPF control:
/// the log monitor's sources, the diagram's palette and anything else built on TreeView all read the
/// same way, and a second adapter per product would be the same code written differently.
/// </para>
/// </remarks>
public sealed class WpfAutomationModule(WpfNodeBinder binder) : IUiAutomationModule
{
	private readonly WpfNodeBinder _binder = binder ?? throw new ArgumentNullException(nameof(binder));

	/// <inheritdoc />
	public string ModuleId => "wpf.standard";

	/// <inheritdoc />
	public IDisposable Register(IUiAdapterRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);

		var adapters = new[]
		{
			// The fallback is registered for FrameworkElement, not Control: a TextBlock, a Panel and a
			// Border are no Controls here, and a fallback that missed them would leave most of a screen
			// unreadable.
			registry.Register(
				"wpf.control",
				typeof(FrameworkElement),
				new WpfControlAdapter(_binder),
				priority: int.MinValue),
			registry.Register(
				"wpf.tree",
				typeof(TreeView),
				new WpfTreeAdapter(_binder),
				priority: 0),
			// A list, so that an item of it can be pointed at: its items are values rather than records,
			// and there is nothing else to name one by.
			registry.Register(
				"wpf.list",
				typeof(ItemsControl),
				new WpfListAdapter(_binder),
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
