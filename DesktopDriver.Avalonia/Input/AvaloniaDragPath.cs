namespace StockSharp.DesktopDriver.Avalonia;

using System;
using System.Collections.Generic;

using global::Avalonia;

/// <summary>
/// The points a drag passes through on its way from where it is pressed to where it is let go.
/// </summary>
/// <remarks>
/// A hand does not jump: a control that starts dragging only once the pointer has moved a few pixels, or that scrolls
/// while the pointer is held near its edge, sees the pointer get there in steps.
/// </remarks>
public static class AvaloniaDragPath
{
	private const double _stride = 8d;

	/// <summary>
	/// The points after the start, the last of them the end itself.
	/// </summary>
	/// <param name="start">Where the drag is pressed.</param>
	/// <param name="end">Where it is let go.</param>
	/// <returns>At least two points, none of them further than a few pixels from the one before.</returns>
	public static IEnumerable<Point> Between(Point start, Point end)
	{
		var dx = end.X - start.X;
		var dy = end.Y - start.Y;
		var steps = Math.Max(2, (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / _stride));

		for (var step = 1; step <= steps; step++)
		{
			var share = (double)step / steps;

			yield return new Point(start.X + dx * share, start.Y + dy * share);
		}
	}
}
