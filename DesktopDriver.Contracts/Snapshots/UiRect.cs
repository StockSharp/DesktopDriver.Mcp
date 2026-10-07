namespace StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// A rectangle in device independent pixels, relative to the surface the node is drawn on.
/// </summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
/// <remarks>
/// Coordinates are surface-relative rather than screen-relative so that a panel keeps the same geometry
/// when its window is moved. Turning them into screen pixels is the input backend's job, because only it
/// knows the scaling of the display the window is on.
/// </remarks>
public sealed record UiRect(double X, double Y, double Width, double Height)
{
	/// <summary>Right edge.</summary>
	public double Right => X + Width;

	/// <summary>Bottom edge.</summary>
	public double Bottom => Y + Height;

	/// <summary>Whether the rectangle has any area at all.</summary>
	public bool IsEmpty => Width <= 0 || Height <= 0;
}
