namespace StockSharp.DesktopDriver.Avalonia;

using System;

using global::Avalonia.Controls;

using StockSharp.DesktopDriver.Adapters;

/// <summary>
/// Registers what can be read out of an ordinary Avalonia control.
/// </summary>
/// <remarks>
/// The fallback, at the lowest priority, covers every control; anything written for a particular control
/// - a grid, a chart, a dock - is registered by its own module and wins over it.
/// <para>
/// A tree is registered here rather than in a product module because a tree is an ordinary Avalonia
/// control: the log monitor's sources, the diagram's palette and anything else built on TreeView all
/// read the same way, and a second adapter per product would be the same code written differently.
/// </para>
/// </remarks>
public sealed class AvaloniaAutomationModule(AvaloniaNodeBinder binder) : IUiAutomationModule
{
	private readonly AvaloniaNodeBinder _binder = binder ?? throw new ArgumentNullException(nameof(binder));

	/// <inheritdoc />
	public string ModuleId => "avalonia.standard";

	/// <inheritdoc />
	public IDisposable Register(IUiAdapterRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);

		var adapters = new[]
		{
			registry.Register(
				"avalonia.control",
				typeof(Control),
				new AvaloniaControlAdapter(_binder),
				priority: int.MinValue),
			registry.Register(
				"avalonia.tree",
				typeof(TreeView),
				new AvaloniaTreeAdapter(_binder),
				priority: 0),
			// A list, so that an item of it can be pointed at: its items are values rather than records, and
			// there is nothing else to name one by.
			registry.Register(
				"avalonia.list",
				typeof(ItemsControl),
				new AvaloniaListAdapter(_binder),
				priority: 10),
			// A picker of a date or a time holds a value rather than text, so setting one is the control's
			// own business and not a matter of keystrokes.
			registry.Register(
				"avalonia.picker",
				typeof(DatePicker),
				new AvaloniaPickerAdapter(_binder),
				priority: 0),
			registry.Register(
				"avalonia.calendarPicker",
				typeof(CalendarDatePicker),
				new AvaloniaPickerAdapter(_binder),
				priority: 0),
			registry.Register(
				"avalonia.timePicker",
				typeof(TimePicker),
				new AvaloniaPickerAdapter(_binder),
				priority: 0),
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
