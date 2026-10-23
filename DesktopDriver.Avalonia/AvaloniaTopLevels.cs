namespace StockSharp.DesktopDriver.Avalonia;

using System.Collections.Generic;
using System.Linq;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.VisualTree;

/// <summary>
/// The window or popup a visual belongs to, the frame a window draws itself included.
/// </summary>
/// <remarks>
/// A window that draws its own title bar keeps it beside itself rather than inside: the window and the layers of its
/// frame - the border drawn under it, the title bar and its buttons over it - are children of one host. The toolkit's
/// own answer to which window a visual is in knows nothing of those layers, yet a person sees them as the window's,
/// closes the window with them, and finds the window's controls covered by them.
/// </remarks>
public static class AvaloniaTopLevels
{
	/// <summary>
	/// The window or popup a visual is shown in.
	/// </summary>
	/// <param name="visual">The visual.</param>
	/// <returns>The window or popup, or nothing when the visual is shown in none.</returns>
	public static TopLevel Of(Visual visual)
	{
		if (visual is null)
			return null;

		if (TopLevel.GetTopLevel(visual) is { } root)
			return root;

		// A layer of a window's frame: the window is the top level beside it, under the host both share.
		var host = visual;

		while (host.GetVisualParent() is { } parent)
			host = parent;

		return host.GetVisualChildren().OfType<TopLevel>().FirstOrDefault();
	}

	/// <summary>
	/// The layers of the frame a window draws itself, in the order they are drawn.
	/// </summary>
	/// <param name="root">The window or popup.</param>
	/// <returns>What is drawn under it, and what is drawn over it; both empty when it draws no frame.</returns>
	public static (IReadOnlyList<Control> Below, IReadOnlyList<Control> Above) FrameOf(TopLevel root)
	{
		var below = new List<Control>();
		var above = new List<Control>();

		if (root?.GetVisualParent() is not { } host)
			return (below, above);

		var passed = false;

		foreach (var layer in host.GetVisualChildren().OfType<Control>())
		{
			if (ReferenceEquals(layer, root))
				passed = true;
			else
				(passed ? above : below).Add(layer);
		}

		return (below, above);
	}

	/// <summary>
	/// Everything a window shows: the visuals inside it and those of the frame it draws itself.
	/// </summary>
	/// <param name="root">The window or popup.</param>
	/// <returns>The visuals, the window itself left out.</returns>
	public static IEnumerable<Visual> VisualsOf(TopLevel root)
	{
		var (below, above) = FrameOf(root);

		return below
			.SelectMany(layer => layer.GetVisualDescendants().Prepend(layer))
			.Concat(root.GetVisualDescendants())
			.Concat(above.SelectMany(layer => layer.GetVisualDescendants().Prepend(layer)));
	}

	/// <summary>
	/// What is on top at a point of a window: one of its controls, or the part of its frame drawn over them.
	/// </summary>
	/// <param name="root">The window or popup.</param>
	/// <param name="point">The point, in the window's coordinates.</param>
	/// <returns>What a pointer there would reach, or nothing.</returns>
	public static IInputElement HitTest(TopLevel root, Point point)
	{
		if (root.GetVisualParent() is InputElement host && root.TranslatePoint(point, host) is { } onHost)
			return host.InputHitTest(onHost);

		return root.InputHitTest(point);
	}
}
