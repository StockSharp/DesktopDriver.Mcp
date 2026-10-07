namespace StockSharp.DesktopDriver.Avalonia.Docking;

using System;

using global::Avalonia.Controls;

using Dock.Avalonia.Controls;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Avalonia;

/// <summary>
/// Reads every Dock.Avalonia <see cref="DockControl"/> of an application as a workspace.
/// </summary>
/// <param name="binder">The binder of the application the module is registered in.</param>
public sealed class DockAutomationModule(AvaloniaNodeBinder binder) : IUiAutomationModule
{
	private readonly AvaloniaNodeBinder _binder = binder ?? throw new ArgumentNullException(nameof(binder));

	/// <inheritdoc />
	public string ModuleId => "avalonia.dock";

	/// <inheritdoc />
	public IDisposable Register(IUiAdapterRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);

		Func<Control, string> rule = control => control is DockControl ? "workspace" : null;

		_binder.KindRules.Add(rule);

		// A workspace says what it shows on the factory its layout is driven through, not on the control.
		UiViewWatch view = DockViewRevisions.Watch;

		_binder.ViewRules.Add(view);

		var adapter = registry.Register("avalonia.dock", typeof(DockControl), new DockAdapter(_binder), priority: 100);

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
