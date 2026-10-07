namespace StockSharp.DesktopDriver.Snapshots;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// A window or popup that nodes are drawn on.
/// </summary>
/// <param name="SurfaceId">The surface's identifier.</param>
/// <param name="Kind">Whether it is a window or a popup.</param>
/// <param name="OwnerSurfaceId">The surface it belongs to, for popups and floating windows.</param>
/// <param name="Root">The topmost node on it.</param>
/// <param name="Title">Its title.</param>
/// <param name="ClientBoundsDip">Its client area.</param>
/// <param name="RenderScaling">The scaling of the display it is on.</param>
/// <param name="IsActive">Whether it is the active surface.</param>
/// <remarks>
/// Surfaces are tracked separately from the tree because a floating panel changes surface without
/// becoming a different document, and a popup is a surface of its own that no window owns in the tree.
/// </remarks>
public sealed record UiSurfaceInfo(
	string SurfaceId,
	string Kind,
	string OwnerSurfaceId,
	UiNodeRef Root,
	UiField<string> Title,
	UiField<UiRect> ClientBoundsDip,
	UiField<double> RenderScaling,
	UiField<bool> IsActive);
