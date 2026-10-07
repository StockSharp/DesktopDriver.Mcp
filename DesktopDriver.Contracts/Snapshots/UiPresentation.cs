namespace StockSharp.DesktopDriver.Snapshots;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// What a node looks like to the windowing system, as opposed to what it holds.
/// </summary>
/// <param name="ContentStatus">Whether the visual exists at all.</param>
/// <param name="SurfaceId">The window or popup it is drawn on.</param>
/// <param name="IsAttached">Whether it is in a live visual tree.</param>
/// <param name="IsEnabled">Whether it would take input.</param>
/// <param name="IsEffectivelyVisible">Whether it and all its ancestors are visible.</param>
/// <param name="IsHitTestVisible">Whether it participates in hit testing.</param>
/// <param name="HasFocus">Whether keyboard input would go to it.</param>
/// <param name="BoundsInSurfaceDip">Where it is on its surface.</param>
/// <param name="RenderScaling">The scaling of the display its surface is on.</param>
/// <remarks>
/// None of this says the user can see the node: another window may be over it, and a visible control can
/// still be painted the wrong colour. Visibility here is the windowing system's answer, not the eye's.
/// </remarks>
public sealed record UiPresentation(
	UiContentStatuses ContentStatus,
	UiField<string> SurfaceId,
	UiField<bool> IsAttached,
	UiField<bool> IsEnabled,
	UiField<bool> IsEffectivelyVisible,
	UiField<bool> IsHitTestVisible,
	UiField<bool> HasFocus,
	UiField<UiRect> BoundsInSurfaceDip,
	UiField<double> RenderScaling);
