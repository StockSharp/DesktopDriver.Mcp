namespace StockSharp.DesktopDriver.Wpf;

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Works out the point on the window where an action aimed at a control will land.
/// </summary>
/// <remarks>
/// The centre of a control is the obvious point and often the wrong one: a control can be partly behind
/// a scroll viewport, shaped so that its centre is a hole, or covered by something else. So the point is
/// chosen by asking the window what is actually under it, and a point that lands on something unrelated
/// is refused rather than clicked.
/// <para>
/// Landing on a child of the target is success: clicking a button means clicking the text inside it, and
/// that is what a person does too.
/// </para>
/// </remarks>
public sealed class WpfInputTargetResolver : IUiInputTargetResolver
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

		if (subject.Instance is not FrameworkElement control)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, "That node is not a control.");

		return Locate(control, context?.Node, context?.Revisions, "control");
	}

	/// <summary>
	/// Works out where an action aimed at one visual will land.
	/// </summary>
	/// <param name="control">The visual the action is aimed at.</param>
	/// <param name="node">The node the receipt names.</param>
	/// <param name="revisions">What that node's versions were when this was worked out.</param>
	/// <param name="what">What the visual is, for the refusal when it cannot be reached.</param>
	/// <returns>Where the action will land.</returns>
	/// <remarks>
	/// Shared with the adapters that find a part of their own control - a column header, a cell, a tab.
	/// Once such an adapter has found the visual, reaching it is the same question as for any other, and
	/// answering it twice would let the two answers differ.
	/// </remarks>
	public static UiResolvedInputTarget Locate(FrameworkElement control, UiNodeRef node, UiRevisions revisions, string what)
	{
		ArgumentNullException.ThrowIfNull(control);

		// The root of the visual tree the control is in rather than its window: a popup is drawn on a
		// surface of its own, and its contents are placed against that surface.
		if (PresentationSource.FromVisual(control)?.RootVisual is not UIElement root)
			throw UiErrors.Fail(UiErrorCodes.NotCreated, $"That {what} is not in a window.");

		if (!control.IsVisible)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, $"That {what} is not visible.");

		if (!control.IsEnabled)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, $"That {what} is disabled.");

		if (!control.IsHitTestVisible)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, $"That {what} does not take pointer input.");

		if (!control.IsArrangeValid)
			throw UiErrors.Fail(UiErrorCodes.NotCreated, $"That {what} has not been laid out.");

		var origin = control.TransformToAncestor(root).Transform(default);
		var bounds = new Rect(origin, control.RenderSize);

		if (bounds.Width <= 0 || bounds.Height <= 0)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, $"That {what} has no size.");

		var point = FindPoint(root, control, bounds)
			?? throw UiErrors.Fail(
				UiErrorCodes.NotInteractable,
				$"Every point of that {what} is covered by something else. " +
				$"At its centre the window says {Describe(root.InputHitTest(Center(bounds)))} is on top.");

		return new UiResolvedInputTarget(
			node,
			UiAutomationNames.GetSurfaceId(root as FrameworkElement),
			new UiRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
			new UiPoint(point.X, point.Y),
			revisions);
	}

	private static Point? FindPoint(UIElement root, FrameworkElement control, Rect bounds)
	{
		foreach (var candidate in Candidates(bounds))
		{
			var hit = root.InputHitTest(candidate);

			if (hit is Visual visual && IsSelfOrDescendant(visual, control))
				return candidate;
		}

		return null;
	}

	// The centre first, then further in from each edge: a control whose centre is covered is usually
	// still reachable, and trying a few named points keeps this predictable rather than a search.
	private static IEnumerable<Point> Candidates(Rect bounds)
	{
		var centre = Center(bounds);

		yield return centre;

		var insetX = Math.Min(bounds.Width / 4, 8);
		var insetY = Math.Min(bounds.Height / 4, 8);

		yield return new Point(bounds.X + insetX, centre.Y);
		yield return new Point(bounds.Right - insetX, centre.Y);
		yield return new Point(centre.X, bounds.Y + insetY);
		yield return new Point(centre.X, bounds.Bottom - insetY);
	}

	private static Point Center(Rect bounds) => new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);

	// Named by what it is rather than by its type alone: "nothing" and "the window itself" are the two
	// answers that mean the interface has not been drawn, and they look the same in a type name.
	private static string Describe(IInputElement hit) => hit switch
	{
		null => "nothing",
		FrameworkElement named when !string.IsNullOrEmpty(named.Name) => $"'{named.Name}' ({named.GetType().Name})",
		Visual visual => visual.GetType().Name,
		_ => hit.GetType().Name,
	};

	private static bool IsSelfOrDescendant(Visual hit, FrameworkElement control)
	{
		for (DependencyObject current = hit; current is not null; current = VisualTreeHelper.GetParent(current))
		{
			if (ReferenceEquals(current, control))
				return true;
		}

		return false;
	}
}
