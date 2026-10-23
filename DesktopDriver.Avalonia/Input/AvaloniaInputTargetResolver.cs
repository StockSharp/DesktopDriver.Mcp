namespace StockSharp.DesktopDriver.Avalonia;

using System;
using System.Collections.Generic;
using System.Linq;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;
using global::Avalonia.VisualTree;

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
public sealed class AvaloniaInputTargetResolver : IUiInputTargetResolver
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

		if (subject.Instance is not Control control)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, "That node is not a control.");

		if (action is UiEnsureVisibleAction)
			return Show(control, context?.Node, context?.Revisions);

		// The pointer leaves the window from wherever it is in it: no point of the control is aimed at, so whatever covers
		// its middle - a window's own content, for one - is no refusal.
		if (action is UiLeaveAction)
			return Placed(control, context?.Node, context?.Revisions, "control");

		return Locate(control, context?.Node, context?.Revisions, "control");
	}

	// The scroll viewers the control sits in are asked to bring it into view, as the parts of tables and trees
	// are shown by their own controls.
	private static UiResolvedInputTarget Show(Control control, UiNodeRef node, UiRevisions revisions)
	{
		if (!control.IsEffectivelyVisible)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, "That control is not visible.");

		control.BringIntoView();
		AvaloniaTopLevels.Of(control)?.UpdateLayout();

		return Placed(control, node, revisions, "control");
	}

	/// <summary>
	/// Where a part brought onto the screen stands now, for the action that brought it there.
	/// </summary>
	/// <param name="control">The visual of the part.</param>
	/// <param name="node">The node the receipt names.</param>
	/// <param name="revisions">What that node's versions were when this was worked out.</param>
	/// <param name="what">What the visual is, for the refusal when it is in no window.</param>
	/// <returns>Where the part is.</returns>
	/// <remarks>
	/// Nothing is sent to it, so nothing is asked about what lies over it - and nothing could be: the window has laid the
	/// part out where it now is but not drawn it there yet, and what is under a point is answered from what was drawn.
	/// The action that presses it next is aimed once the window has drawn it.
	/// </remarks>
	public static UiResolvedInputTarget Placed(Control control, UiNodeRef node, UiRevisions revisions, string what)
	{
		ArgumentNullException.ThrowIfNull(control);

		var root = AvaloniaTopLevels.Of(control)
			?? throw UiErrors.Fail(UiErrorCodes.NotCreated, $"That {what} is not in a window.");

		if (control.TranslatePoint(default, root) is not Point origin)
			throw UiErrors.Fail(UiErrorCodes.NotCreated, $"That {what} has not been laid out.");

		var bounds = new Rect(origin, control.Bounds.Size);

		return new UiResolvedInputTarget(
			node,
			UiAutomationNames.GetSurfaceId(root),
			new UiRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
			new UiPoint(bounds.Center.X, bounds.Center.Y),
			revisions);
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
	public static UiResolvedInputTarget Locate(Control control, UiNodeRef node, UiRevisions revisions, string what)
		=> Locate(control, node, revisions, what, static _ => true);

	/// <summary>
	/// Works out where an action aimed at one visual will land, on a point that reaches the part of it meant.
	/// </summary>
	/// <param name="control">The visual the action is aimed at.</param>
	/// <param name="node">The node the receipt names.</param>
	/// <param name="revisions">What that node's versions were when this was worked out.</param>
	/// <param name="what">What the visual is, for the refusal when it cannot be reached.</param>
	/// <param name="reaches">Whether a pointer landing on a visual inside it reaches what is meant: a tab is not pressed
	/// by pressing the close button it carries.</param>
	/// <returns>Where the action will land.</returns>
	public static UiResolvedInputTarget Locate(Control control, UiNodeRef node, UiRevisions revisions, string what, Func<Visual, bool> reaches)
		=> Locate(control, node, revisions, what, reaches, null);

	/// <summary>
	/// Works out where an action aimed at one visual will land, trying first the part of it a person aims at.
	/// </summary>
	/// <param name="control">The visual the action is aimed at.</param>
	/// <param name="node">The node the receipt names.</param>
	/// <param name="revisions">What that node's versions were when this was worked out.</param>
	/// <param name="what">What the visual is, for the refusal when it cannot be reached.</param>
	/// <param name="reaches">Whether a pointer landing on a visual inside it reaches what is meant.</param>
	/// <param name="aim">A visual inside it a person aims at - the title of a tab - or nothing: a control whose middle is
	/// bare is pressed where it says what it is.</param>
	/// <returns>Where the action will land.</returns>
	public static UiResolvedInputTarget Locate(Control control, UiNodeRef node, UiRevisions revisions, string what, Func<Visual, bool> reaches, Visual aim)
	{
		ArgumentNullException.ThrowIfNull(control);
		ArgumentNullException.ThrowIfNull(reaches);

		var (root, bounds) = Reachable(control, what);

		// Aimed at what a person can see of it: a cell wider than its table, or a row the table shows only the top of, has
		// its middle where nothing of it is drawn.
		var seen = Visible(control, root, bounds);

		var point = (aim is not null && DrawnIn(aim, root) is { Width: > 0, Height: > 0 } aimed ? FindPoint(root, control, aimed, reaches) : null)
			?? FindPoint(root, control, seen, reaches)
			?? FindPoint(root, control, bounds, reaches)
			?? throw UiErrors.Fail(
				UiErrorCodes.NotInteractable,
				$"Every point of that {what} is covered by something else. " +
				$"At the centre of what is drawn of it ({Centre(seen, bounds).X:0}, {Centre(seen, bounds).Y:0}) the window says " +
				$"{Describe(AvaloniaTopLevels.HitTest(root, Centre(seen, bounds)))} is on top.");

		return new UiResolvedInputTarget(
			node,
			UiAutomationNames.GetSurfaceId(root),
			new UiRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
			new UiPoint(point.X, point.Y),
			revisions);
	}

	/// <summary>
	/// Works out where an action aimed at one point of a control will land: that point, or a refusal when the control
	/// is not what the window has there.
	/// </summary>
	/// <param name="control">The control.</param>
	/// <param name="pointInControl">The point, in the control's own coordinates.</param>
	/// <param name="node">The node the receipt names.</param>
	/// <param name="revisions">What that node's versions were when this was worked out.</param>
	/// <param name="what">What is at the point, for the refusal when it cannot be reached.</param>
	/// <returns>Where the action will land.</returns>
	/// <remarks>
	/// For a control that draws its own parts - the price levels of a chart and the orders on them - where a part is, is
	/// known to the control alone, and no other point of it would do: a price a few pixels off is another price.
	/// </remarks>
	public static UiResolvedInputTarget LocateAt(Control control, Point pointInControl, UiNodeRef node, UiRevisions revisions, string what)
	{
		ArgumentNullException.ThrowIfNull(control);

		var (root, bounds) = Reachable(control, "control");

		if (!new Rect(control.Bounds.Size).Contains(pointInControl))
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, $"The control does not show {what}: it falls outside it.");

		if (control.TranslatePoint(pointInControl, root) is not Point point)
			throw UiErrors.Fail(UiErrorCodes.NotCreated, "That control has not been laid out.");

		if (AvaloniaTopLevels.HitTest(root, point) is not Visual hit || !IsSelfOrDescendant(hit, control))
		{
			throw UiErrors.Fail(
				UiErrorCodes.NotInteractable,
				$"Something else is drawn over {what}: at ({point.X:0}, {point.Y:0}) the window says " +
				$"{Describe(AvaloniaTopLevels.HitTest(root, point))} is on top.");
		}

		return new UiResolvedInputTarget(
			node,
			UiAutomationNames.GetSurfaceId(root),
			new UiRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
			new UiPoint(point.X, point.Y),
			revisions);
	}

	// A control a pointer can reach at all: in a window, shown, enabled, taking pointer input and laid out with a size.
	private static (TopLevel Root, Rect Bounds) Reachable(Control control, string what)
	{
		var root = AvaloniaTopLevels.Of(control)
			?? throw UiErrors.Fail(UiErrorCodes.NotCreated, $"That {what} is not in a window.");

		if (!control.IsEffectivelyVisible)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, $"That {what} is not visible.");

		if (!control.IsEffectivelyEnabled)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, $"That {what} is disabled.");

		if (!control.IsHitTestVisible)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, $"That {what} does not take pointer input.");

		if (control.TranslatePoint(default, root) is not Point origin)
			throw UiErrors.Fail(UiErrorCodes.NotCreated, $"That {what} has not been laid out.");

		var bounds = new Rect(origin, control.Bounds.Size);

		if (bounds.Width <= 0 || bounds.Height <= 0)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, $"That {what} has no size.");

		return (root, bounds);
	}

	private static Point Centre(Rect seen, Rect bounds) => seen is { Width: > 0, Height: > 0 } ? seen.Center : bounds.Center;

	// What the ancestors that clip their contents leave of a control: the part of it that is drawn at all.
	private static Rect Visible(Control control, TopLevel root, Rect bounds)
	{
		var visible = bounds;

		for (var current = control.GetVisualParent(); current is not null && !ReferenceEquals(current, root); current = current.GetVisualParent())
		{
			if (current.ClipToBounds && current.TranslatePoint(default, root) is Point origin)
				visible = visible.Intersect(new Rect(origin, current.Bounds.Size));
		}

		return visible;
	}

	// Where a visual is drawn in the window, turned or not: the box around it as it stands on screen.
	private static Rect? DrawnIn(Visual visual, Visual root)
		=> visual.TransformToVisual(root) is { } transform ? new Rect(visual.Bounds.Size).TransformToAABB(transform) : null;

	private static Point? FindPoint(TopLevel root, Control control, Rect bounds, Func<Visual, bool> reaches)
	{
		foreach (var candidate in Candidates(bounds))
		{
			var hit = AvaloniaTopLevels.HitTest(root, candidate);

			if (hit is Visual visual && IsSelfOrDescendant(visual, control) && reaches(visual))
				return candidate;
		}

		return null;
	}

	// The centre first, then further in from each edge: a control whose centre is covered is usually
	// still reachable, and trying a few named points keeps this predictable rather than a search.
	private static IEnumerable<Point> Candidates(Rect bounds)
	{
		yield return bounds.Center;

		var insetX = Math.Min(bounds.Width / 4, 8);
		var insetY = Math.Min(bounds.Height / 4, 8);

		yield return new Point(bounds.X + insetX, bounds.Center.Y);
		yield return new Point(bounds.Right - insetX, bounds.Center.Y);
		yield return new Point(bounds.Center.X, bounds.Y + insetY);
		yield return new Point(bounds.Center.X, bounds.Bottom - insetY);
	}

	// Named by what it is rather than by its type alone: "nothing" and "the window itself" are the two
	// answers that mean the interface has not been drawn, and they look the same in a type name. What it stands
	// in follows, as far as the nearest controls that say what they are, so that a reader knows where it is.
	private static string Describe(IInputElement hit)
	{
		if (hit is not Visual visual)
			return hit is null ? "nothing" : hit.GetType().Name;

		var said = new List<string> { Name(visual) };

		foreach (var ancestor in visual.GetVisualAncestors().OfType<Control>().Where(control => control is TemplatedControl || !string.IsNullOrEmpty(control.Name)).Take(3))
			said.Add(Name(ancestor));

		return string.Join(" in ", said);

		static string Name(Visual shown)
			=> shown is Control { Name: { Length: > 0 } name } ? $"'{name}' ({shown.GetType().Name})" : shown.GetType().Name;
	}

	private static bool IsSelfOrDescendant(Visual hit, Control control)
	{
		for (var current = hit; current is not null; current = current.GetVisualParent())
		{
			if (ReferenceEquals(current, control))
				return true;
		}

		return false;
	}
}
