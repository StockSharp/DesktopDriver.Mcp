namespace StockSharp.DesktopDriver.Avalonia;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Presenters;
using global::Avalonia.Layout;
using global::Avalonia.Media.TextFormatting;
using global::Avalonia.VisualTree;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// Where the text one control shows is drawn anywhere in its window, where a path of the machine is, and where a
/// pattern matches.
/// </summary>
/// <remarks>
/// The name of whoever is signed in is shown by one control and quoted by others - a line of the log reads
/// "name = '…', eula accepted = True" - so covering the control that holds it leaves it in the picture.
/// What is found here is each run of it, down to the characters, wherever a line of text draws it.
/// <para>
/// A folder of the machine is found by its shape rather than by a control that holds it: a storage shown in a box is
/// quoted by a list of drives and by the log in the same way.
/// </para>
/// </remarks>
internal static class PrivateText
{
	// Where a path of this machine begins: a drive, a share, a home folder. A drive letter counts only where no word runs
	// into it, so "https://" is not one; a time such as "10:30" has no letter before its colon.
	private static readonly Regex _path = new(
		@"(?<![A-Za-z0-9])[A-Za-z]:[\\/]|\\\\[^\\/\s]+[\\/]|/(?:home|Users|root)/|(?<!\w)~[\\/]",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly char[] _lineBreaks = ['\r', '\n'];

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
			ComboBox box => Chosen(box),
			_ => throw UiErrors.Unsupported($"A {source.GetType().Name} shows no text of its own to keep out of a picture."),
		};

		// Not known yet - a status bar still waiting for the sign-in. The same name may already be drawn
		// elsewhere, and with nothing to look for it would be left there.
		if (string.IsNullOrWhiteSpace(text))
			throw UiErrors.Fail(UiErrorCodes.NotCreated, "The text to keep out of the picture is not shown yet, so there is nothing to look for.");

		foreach (var visual in AvaloniaTopLevels.VisualsOf(root))
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

	/// <summary>
	/// Every place a local path is drawn inside a control, in its window's units.
	/// </summary>
	/// <param name="root">The window.</param>
	/// <param name="within">The control whose paths are kept out.</param>
	/// <returns>Where each path is drawn, to the end of its line.</returns>
	public static IEnumerable<Rect> Paths(TopLevel root, Control within)
	{
		ArgumentNullException.ThrowIfNull(root);
		ArgumentNullException.ThrowIfNull(within);

		foreach (var visual in within.GetSelfAndVisualDescendants())
		{
			if (!visual.IsEffectivelyVisible)
				continue;

			var found = visual switch
			{
				TextBlock block => FindPaths(root, block, Shown(block), block.TextLayout, block.Padding),
				TextPresenter presenter => FindPaths(root, presenter, presenter.Text, presenter.TextLayout, default),
				_ => [],
			};

			foreach (var rectangle in found)
				yield return rectangle;
		}
	}

	/// <summary>
	/// Every place inside a control where a pattern matches what a line of text draws, in its window's units.
	/// </summary>
	/// <param name="root">The window.</param>
	/// <param name="within">The control whose lines are searched.</param>
	/// <param name="pattern">What is kept out.</param>
	/// <returns>Where each match is drawn.</returns>
	public static IEnumerable<Rect> Matches(TopLevel root, Control within, Regex pattern)
	{
		ArgumentNullException.ThrowIfNull(root);
		ArgumentNullException.ThrowIfNull(within);
		ArgumentNullException.ThrowIfNull(pattern);

		foreach (var visual in within.GetSelfAndVisualDescendants())
		{
			if (!visual.IsEffectivelyVisible)
				continue;

			var found = visual switch
			{
				TextBlock block => FindMatches(root, block, Shown(block), pattern, block.TextLayout, block.Padding),
				TextPresenter presenter => FindMatches(root, presenter, presenter.Text, pattern, presenter.TextLayout, default),
				_ => [],
			};

			foreach (var rectangle in found)
				yield return rectangle;
		}
	}

	private static string Shown(TextBlock block)
		=> block.Inlines is { Count: > 0 } inlines ? inlines.Text : block.Text;

	// What a box shows of its choice is drawn by a template rather than held by the box: the text of its closed face.
	private static string Chosen(ComboBox box)
		=> box.GetVisualDescendants()
			.OfType<TextBlock>()
			.Where(block => block.IsEffectivelyVisible)
			.Select(Shown)
			.FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

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

	private static IEnumerable<Rect> FindPaths(TopLevel root, Layoutable owner, string shown, TextLayout layout, Thickness padding)
	{
		if (string.IsNullOrEmpty(shown) || layout is null)
			yield break;

		if (owner.TranslatePoint(new(padding.Left, padding.Top + Overflow(owner, layout, padding)), root) is not Point origin)
			yield break;

		foreach (Match path in _path.Matches(shown))
		{
			var end = shown.IndexOfAny(_lineBreaks, path.Index);

			if (end < 0)
				end = shown.Length;

			foreach (var drawn in layout.HitTestTextRange(path.Index, end - path.Index))
				yield return drawn.Translate(origin);
		}
	}

	private static IEnumerable<Rect> FindMatches(TopLevel root, Layoutable owner, string shown, Regex pattern, TextLayout layout, Thickness padding)
	{
		if (string.IsNullOrEmpty(shown) || layout is null)
			yield break;

		if (owner.TranslatePoint(new(padding.Left, padding.Top + Overflow(owner, layout, padding)), root) is not Point origin)
			yield break;

		foreach (Match match in pattern.Matches(shown))
		{
			if (match.Length == 0)
				continue;

			foreach (var drawn in layout.HitTestTextRange(match.Index, match.Length))
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
