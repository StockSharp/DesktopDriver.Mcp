namespace StockSharp.DesktopDriver.Wpf;

using System;
using System.Windows;
using System.Windows.Media;

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
public sealed class WpfPresentationReader : IUiPresentationReader
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

		// The surface is the root of the presentation source a visual is drawn through: the window for
		// everything inside a window, and the popup's own root for everything inside an open popup.
		var source = PresentationSource.FromVisual(visual);
		var root = source?.RootVisual;
		var element = visual as UIElement;
		var attached = root is not null;

		// Enabled and visible are coerced down the tree, so both already answer the effective question.
		return new UiPresentation(
			UiContentStatuses.Created,
			attached
				? UiField<string>.Known(UiAutomationNames.GetSurfaceId(root as FrameworkElement))
				: UiField<string>.Unavailable(UiUnavailableReasons.NotLoaded, "not in a window"),
			UiField<bool>.Known(attached),
			element is null
				? UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "not a control")
				: UiField<bool>.Known(element.IsEnabled),
			element is null
				? UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "not a control")
				: UiField<bool>.Known(element.IsVisible),
			element is null
				? UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "not a control")
				: UiField<bool>.Known(element.IsHitTestVisible),
			element is null
				? UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "not a control")
				: UiField<bool>.Known(element.IsFocused),
			element is null
				? UiField<UiRect>.Unavailable(UiUnavailableReasons.Unsupported, "not a control")
				: ReadBounds(element, root),
			ReadScaling(source));
	}

	// Relative to the surface, so that a panel keeps its geometry when its window is moved. Turning this
	// into screen pixels is the input driver's business: only it knows the scaling of the display.
	private static UiField<UiRect> ReadBounds(UIElement element, Visual root)
	{
		if (root is null)
			return UiField<UiRect>.Unavailable(UiUnavailableReasons.NotLoaded, "not in a window");

		GeneralTransform transform;

		try
		{
			transform = element.TransformToAncestor(root);
		}
		catch (InvalidOperationException)
		{
			// The element is not under the root its surface was read from - it left that tree in between.
			return UiField<UiRect>.Unavailable(UiUnavailableReasons.NotLoaded, "not laid out");
		}

		if (!transform.TryTransform(default, out var origin))
			return UiField<UiRect>.Unavailable(UiUnavailableReasons.NotLoaded, "not laid out");

		var size = element.RenderSize;

		return UiField<UiRect>.Known(new UiRect(origin.X, origin.Y, size.Width, size.Height));
	}

	// Device independent pixels to device pixels: the horizontal scale of that matrix is what the rest of
	// the module calls the surface's scaling.
	private static UiField<double> ReadScaling(PresentationSource source)
	{
		var target = source?.CompositionTarget;

		if (target is null)
			return UiField<double>.Unavailable(UiUnavailableReasons.NotLoaded, "not in a window");

		return UiField<double>.Known(target.TransformToDevice.M11);
	}
}
