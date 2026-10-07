namespace StockSharp.DesktopDriver.Wpf;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

using StockSharp.DesktopDriver.Identity;

/// <summary>
/// How a control on screen gets the address a test will name it by.
/// </summary>
/// <remarks>
/// In order of preference: an automation identifier the interface declares, the element's name, and
/// only then its position among its siblings. The first two survive every rearrangement of the screen;
/// the third is a last resort and is marked as such, because a test written against a position starts
/// pointing at something else the day a control is inserted above it.
/// </remarks>
public static class UiAutomationNames
{
	// Menus nest a few deep; anything past this is a tree that leads back into itself.
	private const int _popupDepth = 16;

	/// <summary>
	/// The scope an element belongs to, set by whatever owns that part of the screen.
	/// </summary>
	/// <remarks>
	/// A dock panel, a dialog or a document sets this on its root so that everything inside it is
	/// addressed relative to it, and keeps its addresses when the panel is moved to another window.
	/// </remarks>
	public static readonly DependencyProperty ScopeIdProperty =
		DependencyProperty.RegisterAttached("ScopeId", typeof(string), typeof(UiAutomationNames));

	/// <summary>
	/// Reads the scope set on an element.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns>The scope, or <see langword="null"/>.</returns>
	public static string GetScopeId(FrameworkElement element) => (string)element?.GetValue(ScopeIdProperty);

	/// <summary>
	/// Declares that an element is the root of a scope.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <param name="value">The scope identifier.</param>
	public static void SetScopeId(FrameworkElement element, string value)
	{
		ArgumentNullException.ThrowIfNull(element);

		element.SetValue(ScopeIdProperty, value);
	}

	/// <summary>
	/// Works out the address of an element.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns>Its address, or <see langword="null"/> when it is not attached to anything.</returns>
	public static UiNodeId GetNodeId(FrameworkElement element)
	{
		if (element is null)
			return null;

		var scopeRoot = FindScopeRoot(element, out var scopeId);

		if (scopeId is null)
			return null;

		return new UiNodeId(scopeId, GetLocalId(element, scopeRoot));
	}

	/// <summary>
	/// Whether an element's address is one a test can rely on.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns><see langword="true"/> when the address comes from a declared identifier or a name.</returns>
	public static bool HasStableId(FrameworkElement element)
		=> element is not null &&
			(!string.IsNullOrEmpty(AutomationProperties.GetAutomationId(element)) ||
				!string.IsNullOrEmpty(element.Name));

	/// <summary>
	/// The identifier a popup surface was given by whatever opened it.
	/// </summary>
	/// <remarks>
	/// Every popup root is the same type, so the type name would make a menu and a tooltip open at the
	/// same moment into one surface. What tells them apart is what opened them, and only the side that
	/// found the popup knows that.
	/// </remarks>
	public static readonly DependencyProperty SurfaceIdProperty =
		DependencyProperty.RegisterAttached("SurfaceId", typeof(string), typeof(UiAutomationNames));

	/// <summary>
	/// Names a popup surface after whatever opened it.
	/// </summary>
	/// <param name="root">The popup root.</param>
	/// <param name="value">The surface identifier.</param>
	public static void SetSurfaceId(FrameworkElement root, string value)
	{
		ArgumentNullException.ThrowIfNull(root);

		root.SetValue(SurfaceIdProperty, value);
	}

	/// <summary>
	/// The identifier of a window or popup as a surface.
	/// </summary>
	/// <param name="root">The window or popup root.</param>
	/// <returns>Its surface identifier.</returns>
	public static string GetSurfaceId(FrameworkElement root)
	{
		if (root is null)
			return null;

		if (root is Window window)
		{
			var name = !string.IsNullOrEmpty(window.Name)
				? window.Name
				: window.GetType().Name;

			return $"window:{name}";
		}

		var declared = (string)root.GetValue(SurfaceIdProperty);

		return string.IsNullOrEmpty(declared) ? $"popup:{root.GetType().Name}" : declared;
	}

	// A popup is a surface of its own and an ordinary walk stops at it, but a menu item belongs to the
	// window whose menu was opened rather than to a scope that lasts only while the menu is down. A popup
	// keeps the contents it was given as its logical child, and the popup itself - or the control it was
	// placed against - is back in the window, so the walk carries on from there. Bounded, because a tree
	// that led back into itself would be a hang rather than a wrong answer.
	private static FrameworkElement FindScopeRoot(FrameworkElement element, out string scopeId)
	{
		for (var hop = 0; element is not null && hop < _popupDepth; hop++)
		{
			for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current) as FrameworkElement)
			{
				var declared = GetScopeId(current) ?? UiAutomationScopes.Of(current);

				if (!string.IsNullOrEmpty(declared))
				{
					scopeId = declared;

					return current;
				}
			}

			var root = GetSurfaceRoot(element);

			if (root is Window || FindOpener(element) is not FrameworkElement opener)
			{
				scopeId = GetSurfaceId(root);

				return root;
			}

			element = opener;
		}

		scopeId = null;

		return null;
	}

	// The surface an element is drawn on: the window it belongs to, or the root a popup draws its
	// contents in. An element attached to nothing is drawn nowhere and has no surface.
	private static FrameworkElement GetSurfaceRoot(FrameworkElement element)
	{
		if (element is null)
			return null;

		return PresentationSource.FromVisual(element)?.RootVisual as FrameworkElement;
	}

	// What continues a walk that ran out of tree inside a popup. The popup is the logical parent of the
	// contents it shows, and it names the control it was placed against, which is the one a caller means
	// when it says where the menu came from.
	private static FrameworkElement FindOpener(FrameworkElement element)
	{
		for (var current = (DependencyObject)element; current is not null; current = VisualTreeHelper.GetParent(current))
		{
			if (current is FrameworkElement { Parent: Popup popup })
				return popup.PlacementTarget as FrameworkElement ?? popup.TemplatedParent as FrameworkElement ?? popup;
		}

		return null;
	}

	private static string GetLocalId(FrameworkElement element, FrameworkElement scopeRoot)
	{
		var declared = AutomationProperties.GetAutomationId(element);

		if (!string.IsNullOrEmpty(declared))
			return declared;

		// A name a control template gave its own part - PART_ContentPresenter and the like - belongs to the
		// template rather than to this window, and every instance of that template carries it. Two of them
		// would be one address: a walk that had passed the first would treat the second as somewhere it had
		// been and never look inside it, and everything in there would vanish from the tree.
		if (!string.IsNullOrEmpty(element.Name) && element.TemplatedParent is null)
			return element.Name;

		return BuildPath(element, scopeRoot);
	}

	// The fallback. It is a path rather than a plain index so that two unnamed buttons in different
	// parts of the same panel do not collide, and it is prefixed so that a reader can see at a glance
	// that this address was derived rather than declared.
	private static string BuildPath(FrameworkElement element, FrameworkElement scopeRoot)
	{
		var path = new StringBuilder();

		for (var current = element; current is not null && current != scopeRoot; current = VisualTreeHelper.GetParent(current) as FrameworkElement)
		{
			var parent = VisualTreeHelper.GetParent(current) as FrameworkElement;
			var index = 0;

			if (parent is not null)
			{
				index = VisualChildren(parent)
					.TakeWhile(child => !ReferenceEquals(child, current))
					.Count(child => child.GetType() == current.GetType());
			}

			path.Insert(0, $"/{current.GetType().Name}[{index}]");
		}

		return path.Length == 0 ? "~root" : "~" + path.ToString();
	}

	private static IEnumerable<FrameworkElement> VisualChildren(DependencyObject parent)
	{
		var count = VisualTreeHelper.GetChildrenCount(parent);

		for (var index = 0; index < count; index++)
		{
			if (VisualTreeHelper.GetChild(parent, index) is FrameworkElement child)
				yield return child;
		}
	}
}
