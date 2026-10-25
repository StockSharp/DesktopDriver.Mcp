namespace StockSharp.DesktopDriver.Avalonia.Docking;

using System;
using System.Collections.Generic;
using System.Linq;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.VisualTree;

using Dock.Avalonia.Controls;
using Dock.Model.Controls;
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
			.FirstOrDefault(item => ReferenceEquals(item.DataContext, dockable) && DockVisuals.IsInAStrip(item));

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
		var dockable = Dockable(layout, panelId);

		var onTab = host.GetVisualDescendants()
			.OfType<Control>()
			.Where(item => ReferenceEquals(item.DataContext, dockable) && DockVisuals.IsInAStrip(item))
			.SelectMany(tab => tab.GetVisualDescendants().OfType<Button>())
			.FirstOrDefault(IsCloseButton);

		// A tool alone in its group shows no tab; the button on the caption over it closes whichever panel of the group
		// is in front, so it is this panel's only while this one is.
		var onCaption = onTab is null && dockable.Owner is IDock { ActiveDockable: var active } group && ReferenceEquals(active, dockable)
			? host.GetVisualDescendants()
				.OfType<ToolChromeControl>()
				.Where(chrome => ReferenceEquals(chrome.DataContext, group))
				.SelectMany(chrome => chrome.GetVisualDescendants().OfType<Button>())
				.FirstOrDefault(IsCloseButton)
			: null;

		return onTab ?? onCaption ?? throw UiErrors.Fail(
			UiErrorCodes.NotInteractable,
			$"'{panelId}' shows no close button: the panel cannot be closed, or its button appears only " +
			"while the pointer is over the tab.");
	}

	private static bool IsCloseButton(Button button)
		=> button.Name == "PART_CloseButton" && button.IsEffectivelyVisible;

	/// <summary>
	/// The pin on the caption over a panel, which tucks the panel away to the edge of the workspace.
	/// </summary>
	/// <param name="host">The control the layout is drawn in.</param>
	/// <param name="layout">The layout.</param>
	/// <param name="panelId">The panel.</param>
	/// <returns>The button.</returns>
	public static Control Pin(Visual host, IDock layout, string panelId)
	{
		var dockable = Dockable(layout, panelId);

		// The caption pins whichever panel of its group is in front, so it is this panel's only while this one is.
		var pin = dockable.Owner is IDock { ActiveDockable: var active } group && ReferenceEquals(active, dockable)
			? host.GetVisualDescendants()
				.OfType<ToolChromeControl>()
				.Where(chrome => ReferenceEquals(chrome.DataContext, group))
				.SelectMany(chrome => chrome.GetVisualDescendants().OfType<Button>())
				.FirstOrDefault(button => button.Name == "PART_PinButton" && button.IsEffectivelyVisible)
			: null;

		return pin ?? throw UiErrors.Fail(
			UiErrorCodes.NotInteractable,
			$"'{panelId}' shows no pin: it is not the panel in front of its group, or it cannot be tucked away.");
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

			// A panel tucked away at an edge is pressed by its button on that edge.
			if (current is IRootDock root)
			{
				foreach (var pinned in DockVisuals.Pinned(root))
					queue.Enqueue(pinned);
			}
		}

		throw UiErrors.Fail(UiErrorCodes.NotFound, $"This workspace holds no panel called '{panelId}'.");
	}
}
