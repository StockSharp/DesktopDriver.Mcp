namespace StockSharp.DesktopDriver.Maui;

using System;
using System.Collections.Generic;

using Microsoft.Maui.Controls;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;

using MauiPoint = Microsoft.Maui.Graphics.Point;
using MauiRect = Microsoft.Maui.Graphics.Rect;
using WinElement = Microsoft.UI.Xaml.FrameworkElement;
using WinUIElement = Microsoft.UI.Xaml.UIElement;

/// <summary>
/// Works out where a pointer has to go to reach an element: a point of it that is on top, so that what a
/// click there lands on is the element and not whatever happens to cover it.
/// </summary>
public sealed class MauiInputTargetResolver : IUiInputTargetResolver
{
	/// <inheritdoc />
	public UiResolvedInputTarget Resolve(
		UiSubject subject,
		UiTargetPart part,
		UiInputAction action,
		UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);
		ArgumentNullException.ThrowIfNull(part);

		if (part is not UiControlPart)
		{
			throw UiErrors.Unsupported(
				$"{part.Kind} is a part of a particular kind of control, and nothing here knows where it is.");
		}

		if (subject.Instance is not Element element)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, "That node is not an element.");

		return Locate(element, context?.Node, context?.Revisions, "element");
	}

	/// <summary>
	/// Where to point at an element.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <param name="node">The node the action names.</param>
	/// <param name="revisions">The node's revisions when it was resolved.</param>
	/// <param name="what">What the element is, for the reason given when it cannot be reached.</param>
	/// <returns>The target.</returns>
	public static UiResolvedInputTarget Locate(Element element, UiNodeRef node, UiRevisions revisions, string what)
	{
		ArgumentNullException.ThrowIfNull(element);

		var window = UiAutomationNames.GetWindow(element)
			?? throw UiErrors.Fail(UiErrorCodes.NotCreated, $"That {what} is not in a window.");

		if (!MauiReach.IsVisible(element))
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, $"That {what} is not visible.");

		if (!MauiReach.IsEnabled(element))
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, $"That {what} is disabled.");

		if (!MauiReach.TakesInput(element))
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, $"That {what} does not take pointer input.");

		var view = MauiPlatform.ViewOf(element)
			?? throw UiErrors.Fail(UiErrorCodes.NotCreated, $"That {what} is not drawn by itself.");

		return Locate(window, view, node, revisions, what);
	}

	/// <summary>
	/// Where to point at a platform control - one a MAUI control draws as a part of itself, such as a row of a
	/// list or an entry of a shell's flyout.
	/// </summary>
	/// <param name="window">The window the control is in.</param>
	/// <param name="view">The platform control.</param>
	/// <param name="node">The node the action names.</param>
	/// <param name="revisions">The node's revisions when it was resolved.</param>
	/// <param name="what">What the control is, for the reason given when it cannot be reached.</param>
	/// <returns>The target.</returns>
	public static UiResolvedInputTarget Locate(Window window, WinElement view, UiNodeRef node, UiRevisions revisions, string what)
	{
		ArgumentNullException.ThrowIfNull(window);
		ArgumentNullException.ThrowIfNull(view);

		if (!MauiPlatform.IsDrawn(view))
			throw UiErrors.Fail(UiErrorCodes.NotCreated, $"That {what} is not shown.");

		if (!view.IsHitTestVisible)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, $"That {what} does not take pointer input.");

		var bounds = MauiPlatform.BoundsInWindow(view)
			?? throw UiErrors.Fail(UiErrorCodes.NotCreated, $"That {what} has not been laid out.");

		if (bounds.Width <= 0 || bounds.Height <= 0)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, $"That {what} has no size.");

		var point = FindPoint(window, view, bounds)
			?? throw UiErrors.Fail(
				UiErrorCodes.NotInteractable,
				$"Every point of that {what} is covered by something else. " +
				$"At its centre the window says {Describe(Topmost(window, Center(bounds)))} is on top.");

		return new UiResolvedInputTarget(
			node,
			UiAutomationNames.GetSurfaceId(window),
			new UiRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
			new UiPoint(point.X, point.Y),
			revisions);
	}

	private static MauiPoint? FindPoint(Window window, WinUIElement view, MauiRect bounds)
	{
		foreach (var candidate in Candidates(bounds))
		{
			if (Topmost(window, candidate) is { } hit && MauiPlatform.IsSelfOrInside(hit, view))
				return candidate;
		}

		return null;
	}

	private static WinUIElement Topmost(Window window, MauiPoint point)
	{
		var hits = MauiPlatform.At(window, point);

		return hits.Count > 0 ? hits[0] : null;
	}

	// The centre first, then further in from each edge: an element whose centre is covered is usually still
	// reachable, and trying a few named points keeps this predictable rather than a search.
	private static IEnumerable<MauiPoint> Candidates(MauiRect bounds)
	{
		var centre = Center(bounds);

		yield return centre;

		var insetX = Math.Min(bounds.Width / 4, 8);
		var insetY = Math.Min(bounds.Height / 4, 8);

		yield return new MauiPoint(bounds.X + insetX, centre.Y);
		yield return new MauiPoint(bounds.Right - insetX, centre.Y);
		yield return new MauiPoint(centre.X, bounds.Y + insetY);
		yield return new MauiPoint(centre.X, bounds.Bottom - insetY);
	}

	private static MauiPoint Center(MauiRect bounds) => new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);

	// Named by what it is rather than by its type alone: "nothing" means the window has not been drawn.
	private static string Describe(WinUIElement hit) => hit switch
	{
		null => "nothing",
		WinElement named when !string.IsNullOrEmpty(named.Name) => $"'{named.Name}' ({named.GetType().Name})",
		_ => hit.GetType().Name,
	};
}
