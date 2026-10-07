namespace StockSharp.DesktopDriver.Maui;

using System;

using Microsoft.Maui.Controls;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

using WinElement = Microsoft.UI.Xaml.FrameworkElement;

/// <summary>
/// Reads where an element is and whether a person could see it and reach it.
/// </summary>
public sealed class MauiPresentationReader : IUiPresentationReader
{
	/// <inheritdoc />
	public UiPresentation Read(UiSubject subject, UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		if (subject.Instance is not Element element)
		{
			return new UiPresentation(
				UiContentStatuses.NotCreated,
				UiField<string>.Unavailable(UiUnavailableReasons.NotCreated, "no element"),
				UiField<bool>.Known(false),
				UiField<bool>.Unavailable(UiUnavailableReasons.NotCreated, "no element"),
				UiField<bool>.Known(false),
				UiField<bool>.Known(false),
				UiField<bool>.Known(false),
				UiField<UiRect>.Unavailable(UiUnavailableReasons.NotCreated, "no element"),
				UiField<double>.Unavailable(UiUnavailableReasons.NotCreated, "no element"));
		}

		var window = UiAutomationNames.GetWindow(element);
		var view = MauiPlatform.ViewOf(element);
		var drawn = MauiPlatform.IsDrawn(view);
		var attached = window is not null && (drawn || element is Window);
		var scaling = MauiPlatform.Scaling(window);

		return new UiPresentation(
			UiContentStatuses.Created,
			attached
				? UiField<string>.Known(UiAutomationNames.GetSurfaceId(window))
				: UiField<string>.Unavailable(UiUnavailableReasons.NotLoaded, "not in a window"),
			UiField<bool>.Known(attached),
			UiField<bool>.Known(MauiReach.IsEnabled(element)),
			UiField<bool>.Known(attached && MauiReach.IsVisible(element) && (drawn || element is Window)),
			element is VisualElement
				? UiField<bool>.Known(MauiReach.TakesInput(element))
				: UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "not drawn by itself"),
			element is VisualElement visual
				? UiField<bool>.Known(visual.IsFocused)
				: UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "not drawn by itself"),
			ReadBounds(element, view),
			scaling is { } known
				? UiField<double>.Known(known)
				: UiField<double>.Unavailable(UiUnavailableReasons.NotLoaded, "not in a window"));
	}

	// Relative to the window's client area, so that a page keeps its geometry when its window is moved.
	// Turning this into screen pixels is the input driver's business: only it knows the scaling of the display.
	private static UiField<UiRect> ReadBounds(Element element, WinElement view)
	{
		if (element is Window window)
		{
			return MauiPlatform.ClientSize(window) is { } size
				? UiField<UiRect>.Known(new UiRect(0, 0, size.Width, size.Height))
				: UiField<UiRect>.Unavailable(UiUnavailableReasons.NotLoaded, "not shown");
		}

		if (view is null)
			return UiField<UiRect>.Unavailable(UiUnavailableReasons.NotCreated, "not drawn by itself");

		return MauiPlatform.BoundsInWindow(view) is { } bounds
			? UiField<UiRect>.Known(new UiRect(bounds.X, bounds.Y, bounds.Width, bounds.Height))
			: UiField<UiRect>.Unavailable(UiUnavailableReasons.NotLoaded, "not laid out");
	}
}
