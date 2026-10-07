namespace StockSharp.DesktopDriver.Avalonia;

using global::Avalonia;

/// <summary>
/// Geometry of visuals, for adapters that report where a part of a control is.
/// </summary>
public static class AvaloniaVisuals
{
	/// <summary>
	/// Where a visual is, in another one's coordinates.
	/// </summary>
	/// <param name="visual">The visual.</param>
	/// <param name="relativeTo">What to measure from.</param>
	/// <returns>The rectangle, or nothing when the two are not connected.</returns>
	public static Rect? BoundsIn(Visual visual, Visual relativeTo)
	{
		if (visual is null || relativeTo is null || visual.TranslatePoint(default, relativeTo) is not Point origin)
			return null;

		return new Rect(origin, visual.Bounds.Size);
	}
}
