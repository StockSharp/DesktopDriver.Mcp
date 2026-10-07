namespace StockSharp.DesktopDriver.Avalonia.Grids;

using System;

using global::Avalonia.Controls;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Avalonia;

/// <summary>
/// Reads every <see cref="DataGrid"/> of an application as a table.
/// </summary>
/// <param name="binder">The binder of the application the module is registered in.</param>
/// <remarks>
/// A grid derived from <see cref="DataGrid"/> is read the same way; a library whose derived grid is a
/// more specific thing - an order book, say - registers its own kind rule and adapter before this module,
/// and the more specific one wins.
/// </remarks>
public sealed class DataGridAutomationModule(AvaloniaNodeBinder binder) : IUiAutomationModule
{
	private readonly AvaloniaNodeBinder _binder = binder ?? throw new ArgumentNullException(nameof(binder));

	/// <inheritdoc />
	public string ModuleId => "avalonia.datagrid";

	/// <inheritdoc />
	public IDisposable Register(IUiAdapterRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);

		// Said twice on purpose, because the two answer different questions: the rule decides what a table
		// is called in the tree, and the adapter decides what can be asked of it.
		Func<Control, string> rule = control => control is DataGrid ? "grid" : null;

		_binder.KindRules.Add(rule);

		// A table says what it shows on its collection view, not on the control itself.
		UiViewWatch view = GridViewRevisions.Watch;

		_binder.ViewRules.Add(view);

		var adapter = registry.Register("avalonia.datagrid", typeof(DataGrid), new DataGridAdapter(_binder), priority: 100);

		return new Registration(_binder, rule, view, adapter);
	}

	private sealed class Registration(
		AvaloniaNodeBinder binder,
		Func<Control, string> rule,
		UiViewWatch view,
		IDisposable adapter)
		: IDisposable
	{
		public void Dispose()
		{
			binder.KindRules.Remove(rule);
			binder.ViewRules.Remove(view);
			adapter.Dispose();
		}
	}
}
