namespace StockSharp.DesktopDriver.Maui;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

using WinRT.Interop;

using MauiPoint = Microsoft.Maui.Graphics.Point;
using MauiRect = Microsoft.Maui.Graphics.Rect;
using MauiSize = Microsoft.Maui.Graphics.Size;
using MauiWindow = Microsoft.Maui.Controls.Window;
using WinDependencyObject = Microsoft.UI.Xaml.DependencyObject;
using WinElement = Microsoft.UI.Xaml.FrameworkElement;
using WinFocusState = Microsoft.UI.Xaml.FocusState;
using WinPoint = Windows.Foundation.Point;
using WinUIElement = Microsoft.UI.Xaml.UIElement;
using WinVisibility = Microsoft.UI.Xaml.Visibility;
using WinWindow = Microsoft.UI.Xaml.Window;

/// <summary>
/// The Windows side of a MAUI element: where it is drawn, what is on top at a point, and the action the
/// platform control itself offers.
/// </summary>
/// <remarks>
/// A MAUI element knows its size but not where its window put it - the flyout, the title bar and the page
/// chrome are all the platform's. Everything measured here is in the window's own device independent pixels,
/// counted from the top left of its client area, which is the surface every rectangle a reader is given is
/// relative to.
/// </remarks>
internal static class MauiPlatform
{
	/// <summary>
	/// The platform window a MAUI window is shown in.
	/// </summary>
	public static WinWindow WindowOf(MauiWindow window) => window?.Handler?.PlatformView as WinWindow;

	/// <summary>
	/// The root of what a window draws: the element every rectangle is measured against.
	/// </summary>
	public static WinElement RootOf(MauiWindow window) => WindowOf(window)?.Content as WinElement;

	/// <summary>
	/// The platform control an element is drawn as, or the wrapper around it when MAUI put one there for a
	/// shadow or a clip - the wrapper is what takes the room on screen.
	/// </summary>
	public static WinElement ViewOf(Element element)
	{
		if (element is MauiWindow window)
			return RootOf(window);

		if (element is not IElement { Handler: { } handler })
			return null;

		if (handler is IViewHandler { ContainerView: WinElement container })
			return container;

		return handler.PlatformView as WinElement;
	}

	/// <summary>
	/// Whether a window is the one the keyboard is on.
	/// </summary>
	/// <returns><see langword="null"/> for a window that is not shown.</returns>
	public static bool? IsForeground(MauiWindow window)
	{
		if (WindowOf(window) is not { } platform)
			return null;

		// Asked of the system rather than followed through the window's own events: those say only when the
		// answer changes, and a window that came up before anybody listened has said nothing yet.
		return WindowNative.GetWindowHandle(platform) == GetForegroundWindow();
	}

	/// <summary>
	/// The client area of a window.
	/// </summary>
	public static MauiSize? ClientSize(MauiWindow window)
		=> RootOf(window) is { } root ? new MauiSize(root.ActualWidth, root.ActualHeight) : null;

	/// <summary>
	/// The scaling of the display a window is on: device pixels for one device independent pixel.
	/// </summary>
	public static double? Scaling(MauiWindow window)
		=> RootOf(window)?.XamlRoot?.RasterizationScale;

	/// <summary>
	/// Where a platform control is in its window.
	/// </summary>
	public static MauiRect? BoundsInWindow(WinElement view)
	{
		if (view?.XamlRoot?.Content is not WinUIElement root)
			return null;

		WinPoint origin;

		try
		{
			origin = view.TransformToVisual(root).TransformPoint(default);
		}
		catch (ArgumentException)
		{
			// The control left the window between being found and being measured.
			return null;
		}

		return new MauiRect(origin.X, origin.Y, view.ActualWidth, view.ActualHeight);
	}

	/// <summary>
	/// Whether a platform control is drawn: in a window, and neither it nor anything it is inside collapsed.
	/// </summary>
	public static bool IsDrawn(WinElement view)
	{
		if (view?.XamlRoot is null || !view.IsLoaded)
			return false;

		for (WinDependencyObject current = view; current is not null; current = VisualTreeHelper.GetParent(current))
		{
			if (current is WinUIElement { Visibility: WinVisibility.Collapsed })
				return false;
		}

		return true;
	}

	/// <summary>
	/// What takes a pointer at a point of a window, the topmost first.
	/// </summary>
	public static IReadOnlyList<WinUIElement> At(MauiWindow window, MauiPoint point)
	{
		if (RootOf(window) is not { } root)
			return [];

		return [.. VisualTreeHelper.FindElementsInHostCoordinates(new WinPoint(point.X, point.Y), root, false)];
	}

	/// <summary>
	/// Whether one platform control is the other or inside it.
	/// </summary>
	public static bool IsSelfOrInside(WinUIElement element, WinUIElement container)
	{
		for (WinDependencyObject current = element; current is not null; current = VisualTreeHelper.GetParent(current))
		{
			if (ReferenceEquals(current, container))
				return true;
		}

		return false;
	}

	/// <summary>
	/// What a platform control is drawn inside.
	/// </summary>
	public static WinUIElement Parent(WinUIElement element)
		=> element is null ? null : VisualTreeHelper.GetParent(element) as WinUIElement;

	/// <summary>
	/// Carries out the action a platform control offers to a pointer: the same one an accessibility tool would
	/// use, and the one that leads to what a person's click leads to.
	/// </summary>
	/// <returns><see langword="true"/> when the control offered one.</returns>
	public static bool Operate(WinUIElement element)
	{
		if (element is null)
			return false;

		var peer = FrameworkElementAutomationPeer.FromElement(element) ?? FrameworkElementAutomationPeer.CreatePeerForElement(element);

		if (peer is null)
			return false;

		// Doing comes first: an entry that does something when it is clicked does that, and being selected or
		// opened is what a click means only for the things that do nothing else.
		if (peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke)
		{
			Focus(element);
			invoke.Invoke();

			return true;
		}

		// A tab and a list item are not invoked but chosen, and what a click on one means is that it becomes
		// the chosen one.
		if (peer.GetPattern(PatternInterface.SelectionItem) is ISelectionItemProvider selection)
		{
			Focus(element);
			selection.Select();

			return true;
		}

		// Toggling before opening: what a click does to a check box is tick it.
		if (peer.GetPattern(PatternInterface.Toggle) is IToggleProvider toggle)
		{
			Focus(element);
			toggle.Toggle();

			return true;
		}

		if (peer.GetPattern(PatternInterface.ExpandCollapse) is IExpandCollapseProvider expand)
		{
			Focus(element);
			expand.Expand();

			return true;
		}

		return false;
	}

	/// <summary>
	/// Whether a platform control is a field a person types into, which a click puts the keyboard on.
	/// </summary>
	public static bool IsTextField(WinUIElement element)
		=> element is TextBox or PasswordBox or RichEditBox or AutoSuggestBox;

	/// <summary>
	/// Puts the keyboard on a platform control, when it takes the keyboard.
	/// </summary>
	public static void Focus(WinUIElement element)
	{
		if (element is Control { IsTabStop: true } control)
			control.Focus(WinFocusState.Programmatic);
	}

	/// <summary>
	/// The nearest scrolling area a platform control is inside.
	/// </summary>
	public static ScrollViewer ScrollerOf(WinUIElement element)
	{
		for (var current = element; current is not null; current = Parent(current))
		{
			if (current is ScrollViewer scroller)
				return scroller;
		}

		return null;
	}

	/// <summary>
	/// The line a platform list draws around a row: what is chosen when the row is clicked.
	/// </summary>
	public static WinElement ContainerOf(WinElement row)
	{
		for (WinUIElement current = row; current is not null; current = Parent(current))
		{
			if (current is SelectorItem or ItemContainer)
				return (WinElement)current;
		}

		return row;
	}

	/// <summary>
	/// Every platform control drawn inside another, nearest first.
	/// </summary>
	public static IEnumerable<WinUIElement> PlatformDescendants(WinUIElement root)
	{
		var pending = new Queue<WinDependencyObject>();

		pending.Enqueue(root);

		while (pending.Count > 0)
		{
			var current = pending.Dequeue();
			var count = VisualTreeHelper.GetChildrenCount(current);

			for (var index = 0; index < count; index++)
			{
				var child = VisualTreeHelper.GetChild(current, index);

				if (child is WinUIElement element)
					yield return element;

				pending.Enqueue(child);
			}
		}
	}

	/// <summary>
	/// Every element of a window and of the pages shown over it.
	/// </summary>
	public static IEnumerable<Element> Descendants(Element element)
	{
		foreach (var child in MauiNodeBinder.Children(element))
		{
			yield return child;

			foreach (var descendant in Descendants(child))
				yield return descendant;
		}
	}

	/// <summary>
	/// The elements of a window that are drawn as the given platform controls.
	/// </summary>
	public static Dictionary<WinUIElement, Element> ElementsDrawnAs(MauiWindow window, IEnumerable<WinUIElement> views)
	{
		var wanted = new HashSet<WinUIElement>(views);
		var found = new Dictionary<WinUIElement, Element>();

		foreach (var element in Descendants(window))
		{
			if (element is not IElement { Handler: { } handler })
				continue;

			// The wrapper and the control inside it are both what the element is drawn as.
			if (handler is IViewHandler { ContainerView: WinUIElement container } && wanted.Contains(container))
				found.TryAdd(container, element);

			if (handler.PlatformView is WinUIElement platform && wanted.Contains(platform))
				found.TryAdd(platform, element);
		}

		return found;
	}

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();
}
