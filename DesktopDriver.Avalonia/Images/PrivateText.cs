namespace StockSharp.DesktopDriver.Avalonia;

using System;
using System.Collections.Generic;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Presenters;
using global::Avalonia.Layout;
using global::Avalonia.Media.TextFormatting;
using global::Avalonia.VisualTree;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// Where the text one control shows is drawn anywhere in its window.
/// </summary>
/// <remarks>
/// The name of whoever is signed in is shown by one control and quoted by others - a line of the log reads
/// "name = '…', eula accepted = True" - so covering the control that holds it leaves it in the picture.
/// What is found here is each run of it, down to the characters, wherever a line of text draws it.
/// </remarks>
internal static class PrivateText
{
	/// <summary>
	/// Every place the text of a control is drawn in a window, in the window's units.
	/// </summary>
	/// <param name="root">The window.</param>
	/// <param name="source">The control whose text is private.</param>
	/// <returns>Where each run of that text is drawn.</returns>
	public static IEnumerable<Rect> Occurrences(TopLevel root, Control source)
	{
		ArgumentNullException.ThrowIfNull(root);
		ArgumentNullException.ThrowIfNull(source);

		var text = source switch
		{
			TextBlock block => Shown(block),
			TextBox box => box.Text,
			ContentControl { Content: string content } => content,
			_ => throw UiErrors.Unsupported($"A {source.GetType().Name} shows no text of its own to keep out of a picture."),
		};

		// Not known yet - a status bar still waiting for the sign-in. The same name may already be drawn
		// elsewhere, and with nothing to look for it would be left there.
		if (string.IsNullOrWhiteSpace(text))
			throw UiErrors.Fail(UiErrorCodes.NotCreated, "The text to keep out of the picture is not shown yet, so there is nothing to look for.");

		foreach (var visual in root.GetVisualDescendants())
		{
			if (!visual.IsEffectivelyVisible)
				continue;

			var found = visual switch
			{
				TextBlock block => Find(root, block, Shown(block), text, block.TextLayout, block.Padding),
				TextPresenter presenter => Find(root, presenter, presenter.Text, text, presenter.TextLayout, default),
				_ => [],
			};

			foreach (var rectangle in found)
				yield return rectangle;
		}
	}

	private static string Shown(TextBlock block)
		=> block.Inlines is { Count: > 0 } inlines ? inlines.Text : block.Text;

	private static IEnumerable<Rect> Find(TopLevel root, Layoutable owner, string shown, string text, TextLayout layout, Thickness padding)
	{
		if (string.IsNullOrEmpty(shown) || layout is null)
			yield break;

		if (owner.TranslatePoint(new(padding.Left, padding.Top + Overflow(owner, layout, padding)), root) is not Point origin)
			yield break;

		for (var start = shown.IndexOf(text, StringComparison.OrdinalIgnoreCase);
			start >= 0;
			start = shown.IndexOf(text, start + text.Length, StringComparison.OrdinalIgnoreCase))
		{
			foreach (var drawn in layout.HitTestTextRange(start, text.Length))
				yield return drawn.Translate(origin);
		}
	}

	// A line taller than the box it is given is moved up by the box's vertical alignment before it is
	// drawn, and its runs move with it.
	private static double Overflow(Layoutable owner, TextLayout layout, Thickness padding)
	{
		var room = owner.Bounds.Height - padding.Top - padding.Bottom;

		if (room >= layout.Height)
			return 0;

		return owner.VerticalAlignment switch
		{
			VerticalAlignment.Center => (room - layout.Height) / 2,
			VerticalAlignment.Bottom => room - layout.Height,
			_ => 0,
		};
	}
}
