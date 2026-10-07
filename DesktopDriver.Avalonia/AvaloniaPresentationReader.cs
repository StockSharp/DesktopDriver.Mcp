namespace StockSharp.DesktopDriver.Avalonia;

using System;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.VisualTree;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Reads where a control is and whether it would take input.
/// </summary>
/// <remarks>
/// None of this says the user can see the control: another window may be over it, and a control that is
/// visible can still be painted the wrong colour. What it answers is the windowing system's question -
/// is this attached, enabled, laid out, and where - which is what deciding where to click needs.
/// </remarks>
public sealed class AvaloniaPresentationReader : IUiPresentationReader
{
	/// <inheritdoc />
	public UiPresentation Read(UiSubject subject, UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		if (subject.Instance is not Visual visual)
		{
			return new UiPresentation(
				UiContentStatuses.NotCreated,
				UiField<string>.Unavailable(UiUnavailableReasons.NotCreated, "no visual"),
				UiField<bool>.Known(false),
				UiField<bool>.Unavailable(UiUnavailableReasons.NotCreated, "no visual"),
				UiField<bool>.Known(false),
				UiField<bool>.Known(false),
				UiField<bool>.Known(false),
				UiField<UiRect>.Unavailable(UiUnavailableReasons.NotCreated, "no visual"),
				UiField<double>.Unavailable(UiUnavailableReasons.NotCreated, "no visual"));
		}

		var root = TopLevel.GetTopLevel(visual as Control);
		var control = visual as Control;
		var attached = root is not null;

		return new UiPresentation(
			UiContentStatuses.Created,
			attached
				? UiField<string>.Known(UiAutomationNames.GetSurfaceId(root))
				: UiField<string>.Unavailable(UiUnavailableReasons.NotLoaded, "not in a window"),
			UiField<bool>.Known(attached),
			control is null
				? UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "not a control")
				: UiField<bool>.Known(control.IsEffectivelyEnabled),
			UiField<bool>.Known(visual.IsEffectivelyVisible),
			control is null
				? UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "not a control")
				: UiField<bool>.Known(control.IsHitTestVisible),
			control is null
				? UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "not a control")
				: UiField<bool>.Known(control.IsFocused),
			ReadBounds(visual, root),
			root is not null
				? UiField<double>.Known(root.RenderScaling)
				: UiField<double>.Unavailable(UiUnavailableReasons.NotLoaded, "not in a window"));
	}

	// Relative to the surface, so that a panel keeps its geometry when its window is moved. Turning this
	// into screen pixels is the input driver's business: only it knows the scaling of the display.
	private static UiField<UiRect> ReadBounds(Visual visual, TopLevel root)
	{
		if (root is null)
			return UiField<UiRect>.Unavailable(UiUnavailableReasons.NotLoaded, "not in a window");

		var bounds = visual.Bounds;

		if (visual.TranslatePoint(default, (Visual)root) is not Point origin)
			return UiField<UiRect>.Unavailable(UiUnavailableReasons.NotLoaded, "not laid out");

		return UiField<UiRect>.Known(new UiRect(origin.X, origin.Y, bounds.Width, bounds.Height));
	}
}
