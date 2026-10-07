namespace StockSharp.DesktopDriver.Avalonia;

using System;

using global::Avalonia.Controls;

/// <summary>
/// Subscribes to whatever a library's own control raises when what it shows changes.
/// </summary>
/// <param name="control">The control that was just bound.</param>
/// <param name="changed">To be called when what that control shows, or the order it shows it in, changed.</param>
/// <remarks>
/// A control from Avalonia itself says so through its own properties, and the watcher knows those. A
/// table from another library says it somewhere the watcher cannot name without referencing that library
/// - a collection view, a description collection - so the library says it here instead.
/// <para>
/// A rule that does not recognise the control returns without subscribing to anything.
/// </para>
/// </remarks>
public delegate void UiViewWatch(Control control, Action changed);
