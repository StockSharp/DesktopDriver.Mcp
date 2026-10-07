namespace StockSharp.DesktopDriver.Avalonia.Docking;

using System;
using System.Collections.Generic;
using System.Linq;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.VisualTree;

using Dock.Model.Core;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// Finds the visual behind a named part of a docking workspace.
/// </summary>
/// <remarks>
/// Switching panels, closing one, dragging one out - all of these are done to the tab, not to the panel's
/// content, and the tab is drawn by the docking library rather than by the panel. So a caller names the
/// panel and this finds the strip item the library built for it.
/// </remarks>
internal static class DockParts
{
	/// <summary>
	/// The tab of a panel.
	/// </summary>
	/// <param name="host">The control the layout is drawn in.</param>
	/// <param name="layout">The layout.</param>
	/// <param name="panelId">The panel.</param>
	/// <returns>The tab visual.</returns>
	public static Control Tab(Visual host, IDock layout, string panelId)
	{
		var dockable = Dockable(layout, panelId);

		var tab = host.GetVisualDescendants()
			.OfType<Control>()
			.FirstOrDefault(item => ReferenceEquals(item.DataContext, dockable) && IsInAStrip(item));

		return tab ?? throw UiErrors.Fail(
			UiErrorCodes.NotCreated,
			$"No tab has been built for '{panelId}': the panel is hidden, pinned away or in a group that " +
			"has never been shown.");
	}

	/// <summary>
	/// The close button on a panel's tab.
	/// </summary>
	/// <param name="host">The control the layout is drawn in.</param>
	/// <param name="layout">The layout.</param>
	/// <param name="panelId">The panel.</param>
	/// <returns>The button.</returns>
	public static Control Close(Visual host, IDock layout, string panelId)
	{
		var tab = Tab(host, layout, panelId);

		var button = tab.GetVisualDescendants()
			.OfType<Button>()
			.FirstOrDefault(candidate => candidate.Name == "PART_CloseButton" && candidate.IsEffectivelyVisible);

		return button ?? throw UiErrors.Fail(
			UiErrorCodes.NotInteractable,
			$"'{panelId}' shows no close button: the panel cannot be closed, or its button appears only " +
			"while the pointer is over the tab.");
	}

	/// <summary>
	/// Brings a panel's tab into the strip's visible part.
	/// </summary>
	/// <param name="host">The control the layout is drawn in.</param>
	/// <param name="layout">The layout.</param>
	/// <param name="panelId">The panel.</param>
	public static void Show(Visual host, IDock layout, string panelId)
	{
		var tab = Tab(host, layout, panelId);

		tab.BringIntoView();

		if (host is Control control)
			control.UpdateLayout();
	}

	// A document tab and a tool tab are different controls with different base types, and both of them
	// carry the panel as their data - as does the content that panel is drawn in. What tells the tab from
	// the content is the strip it sits in, which is the one thing every kind of tab has in common.
	private static bool IsInAStrip(Visual item)
	{
		for (var current = item.GetVisualParent(); current is not null; current = current.GetVisualParent())
		{
			if (current.GetType().Name.EndsWith("TabStrip", StringComparison.Ordinal))
				return true;
		}

		return false;
	}

	private static IDockable Dockable(IDock layout, string panelId)
	{
		if (layout is null)
			throw UiErrors.Fail(UiErrorCodes.NotCreated, "This workspace holds no layout yet.");

		var queue = new Queue<IDockable>();
		var seen = new HashSet<IDockable>();

		queue.Enqueue(layout);

		while (queue.Count > 0)
		{
			var current = queue.Dequeue();

			if (!seen.Add(current))
				continue;

			if (string.Equals(current.Id, panelId, StringComparison.Ordinal))
				return current;

			if (current is IDock dock && dock.VisibleDockables is { } children)
			{
				foreach (var child in children)
					queue.Enqueue(child);
			}
		}

		throw UiErrors.Fail(UiErrorCodes.NotFound, $"This workspace holds no panel called '{panelId}'.");
	}
}
