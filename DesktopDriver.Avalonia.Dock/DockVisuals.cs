namespace StockSharp.DesktopDriver.Avalonia.Docking;

using System;
using System.Collections.Generic;
using System.Linq;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Presenters;
using global::Avalonia.VisualTree;

using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;

/// <summary>
/// Finds the control a docked panel's content is drawn in.
/// </summary>
/// <remarks>
/// A docking layout is a model, and the model exists whether or not anything has been built for it. The
/// control is found by looking for the visual whose data is that panel, which is exactly what a docking
/// library builds when it first shows one - so a panel that has never been shown has no control, and
/// saying so is the truth rather than a failure to look hard enough.
/// </remarks>
internal static class DockVisuals
{
	/// <summary>
	/// The content of a panel, when it has been built.
	/// </summary>
	/// <param name="host">The control the layout is drawn in.</param>
	/// <param name="dockable">The panel.</param>
	/// <returns>The content, or <see langword="null"/> when nothing has been built for it.</returns>
	public static Control ContentOf(Visual host, IDockable dockable)
	{
		if (host is null || dockable is null)
			return null;

		foreach (var candidate in host.GetVisualDescendants().OfType<ContentPresenter>())
		{
			if (!ReferenceEquals(candidate.DataContext, dockable) || IsInAStrip(candidate))
				continue;

			// The presenter is the docking library's own scaffolding; what a caller means by the panel's
			// content is the control inside it.
			var content = candidate.GetVisualChildren().OfType<Control>().FirstOrDefault();

			if (content is not null)
				return content;
		}

		return null;
	}

	/// <summary>
	/// Whether a visual belongs to a strip of tabs: the tabs of a group, or the buttons along an edge that a panel
	/// tucked away there slides out from.
	/// </summary>
	/// <param name="visual">The visual.</param>
	/// <returns><see langword="true"/> when it is part of a strip.</returns>
	/// <remarks>
	/// A panel's tab carries the panel as its data, exactly as the panel's content does, and the tab comes first
	/// in the tree. A reader that took the first match read the caption on the tab and called it the panel's
	/// content: it answered with a label where a table was asked for, and every address under it pointed into the
	/// tab strip.
	/// </remarks>
	public static bool IsInAStrip(Visual visual)
	{
		for (var current = visual?.GetVisualParent(); current is not null; current = current.GetVisualParent())
		{
			if (current is ToolPinnedControl || current.GetType().Name.EndsWith("TabStrip", StringComparison.Ordinal))
				return true;
		}

		return false;
	}

	/// <summary>
	/// The panels tucked away at the edges of a workspace, which belong to no group.
	/// </summary>
	/// <param name="root">The root of the layout.</param>
	/// <returns>The panels, edge by edge.</returns>
	public static IEnumerable<IDockable> Pinned(IRootDock root)
		=> new[] { root?.LeftPinnedDockables, root?.RightPinnedDockables, root?.TopPinnedDockables, root?.BottomPinnedDockables }
			.Where(edge => edge is not null)
			.SelectMany(edge => edge);

	/// <summary>
	/// Every window the layout is drawn across, the main one first.
	/// </summary>
	/// <param name="host">The control the layout is drawn in.</param>
	/// <returns>The windows.</returns>
	public static IReadOnlyList<Visual> Surfaces(Visual host)
		=> host is null ? [] : [host];
}
