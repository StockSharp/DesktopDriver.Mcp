namespace StockSharp.DesktopDriver.Avalonia;

using System.Linq;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.VisualTree;

using StockSharp.DesktopDriver.Input;

/// <summary>
/// Where keys and text go when a person types at a control.
/// </summary>
public static class AvaloniaKeyboardTargets
{
	/// <summary>
	/// The control that takes the typing aimed at a control or at a part of it.
	/// </summary>
	/// <param name="part">The part aimed at.</param>
	/// <param name="control">The control the part belongs to.</param>
	/// <param name="root">The window both are in.</param>
	/// <param name="point">The part's point, in the window's coordinates.</param>
	/// <returns>The control named, or the field at the part's point.</returns>
	/// <remarks>
	/// A part of a control - a field inside a row of a tree, a property's editor - is typed into by the field
	/// at its point, the one a click there gives focus to. The field is found by where it lies rather than by a
	/// hit test, which answers from the last frame drawn and so misses rows built since; and it is the innermost
	/// control a view declared there, not a piece of some control's own template.
	/// </remarks>
	public static Control For(UiTargetPart part, Control control, Visual root, Point point)
	{
		if (part is UiControlPart || control is null || root is null)
			return control;

		return control
			.GetVisualDescendants()
			.OfType<Control>()
			.Where(candidate =>
				candidate.TemplatedParent is null &&
				candidate.Focusable &&
				candidate.IsEffectivelyEnabled &&
				candidate.IsEffectivelyVisible &&
				candidate.TranslatePoint(default, root) is { } origin &&
				new Rect(origin, candidate.Bounds.Size).Contains(point))
			.LastOrDefault()
			?? control;
	}

	/// <summary>
	/// The control that takes the text typed at a control or at a part of it.
	/// </summary>
	/// <param name="part">The part aimed at.</param>
	/// <param name="control">The control the part belongs to.</param>
	/// <param name="root">The window both are in.</param>
	/// <param name="point">The part's point, in the window's coordinates.</param>
	/// <returns>The box the text goes into.</returns>
	/// <remarks>
	/// A control that edits text through a box of its own - a list that can be typed in, a number field - takes
	/// text in that box, which is where a click on the control puts the keyboard. The control itself does
	/// nothing with text raised at it.
	/// </remarks>
	public static Control ForText(UiTargetPart part, Control control, Visual root, Point point)
	{
		var target = For(part, control, root, point);

		if (target is null or TextBox)
			return target;

		return target
			.GetVisualDescendants()
			.OfType<TextBox>()
			.FirstOrDefault(box => box.Focusable && box.IsEffectivelyEnabled && box.IsEffectivelyVisible)
			?? target;
	}
}
