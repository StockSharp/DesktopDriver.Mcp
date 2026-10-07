namespace StockSharp.DesktopDriver.Maui;

using System;
using System.Linq;
using System.Text;

using Microsoft.Maui;
using Microsoft.Maui.Controls;

using StockSharp.DesktopDriver.Identity;

/// <summary>
/// Where an element of a MAUI application can be found again: the scope it belongs to and its name in it.
/// </summary>
public static class UiAutomationNames
{
	// Shell names an entry nobody gave a route with one it makes up, and the made-up name moves with the
	// order of the entries - so it is no address.
	private static readonly string[] _generatedRoutes = ["D_FAULT_", "IMPL_"];

	/// <summary>
	/// Declares an element the root of a scope of its own, so that what is inside it is named against it
	/// rather than against the window.
	/// </summary>
	public static readonly BindableProperty ScopeIdProperty =
		BindableProperty.CreateAttached("ScopeId", typeof(string), typeof(UiAutomationNames), null);

	/// <summary>
	/// The scope an element declares.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns>The scope, or <see langword="null"/> when it declares none.</returns>
	public static string GetScopeId(BindableObject element) => (string)element?.GetValue(ScopeIdProperty);

	/// <summary>
	/// Declares an element the root of a scope.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <param name="value">The scope.</param>
	public static void SetScopeId(BindableObject element, string value)
	{
		ArgumentNullException.ThrowIfNull(element);

		element.SetValue(ScopeIdProperty, value);
	}

	/// <summary>
	/// The address of an element.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns>The address, or <see langword="null"/> for an element that is in no window.</returns>
	public static UiNodeId GetNodeId(Element element)
	{
		if (element is null)
			return null;

		var scopeRoot = FindScopeRoot(element, out var scopeId);

		if (scopeId is null)
			return null;

		return new UiNodeId(scopeId, GetLocalId(element, scopeRoot));
	}

	/// <summary>
	/// Whether an element carries a name that was given to it rather than worked out.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns><see langword="true"/> when it does.</returns>
	public static bool HasStableId(Element element)
		=> element is not null && (!string.IsNullOrEmpty(element.AutomationId) || DeclaredRoute(element) is not null);

	/// <summary>
	/// The surface a window is: its name when it was given one, its type otherwise.
	/// </summary>
	/// <param name="window">The window.</param>
	/// <returns>The surface.</returns>
	public static string GetSurfaceId(Window window)
	{
		if (window is null)
			return null;

		var name = !string.IsNullOrEmpty(window.AutomationId)
			? window.AutomationId
			: window.GetType().Name;

		return $"window:{name}";
	}

	/// <summary>
	/// The window an element is in.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns>The window, or <see langword="null"/> for an element that is in none.</returns>
	public static Window GetWindow(Element element)
	{
		for (var current = element; current is not null; current = Parent(current))
		{
			if (current is Window window)
				return window;
		}

		return null;
	}

	/// <summary>
	/// What an element is drawn inside. A page shown over the window has the window above it like any other
	/// page, so the walk from anything on it ends at the window it is shown in.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns>Its parent, or <see langword="null"/> at the top.</returns>
	public static Element Parent(Element element)
		=> element is IVisualTreeElement visual && visual.GetVisualParent() is Element parent
			? parent
			: element?.Parent;

	private static Element FindScopeRoot(Element element, out string scopeId)
	{
		for (var current = element; current is not null; current = Parent(current))
		{
			var declared = GetScopeId(current) ?? UiAutomationScopes.Of(current);

			if (!string.IsNullOrEmpty(declared))
			{
				scopeId = declared;

				return current;
			}

			if (current is Window window)
			{
				scopeId = GetSurfaceId(window);

				return window;
			}
		}

		scopeId = null;

		return null;
	}

	private static string GetLocalId(Element element, Element scopeRoot)
	{
		if (!string.IsNullOrEmpty(element.AutomationId))
			return element.AutomationId;

		if (DeclaredRoute(element) is { } route)
			return route;

		return BuildPath(element, scopeRoot);
	}

	// A shell entry is addressed by the route it was declared with: that is the name the application itself
	// navigates by.
	private static string DeclaredRoute(Element element)
	{
		if (element is not BaseShellItem)
			return null;

		var route = Routing.GetRoute(element);

		if (string.IsNullOrEmpty(route) || _generatedRoutes.Any(prefix => route.StartsWith(prefix, StringComparison.Ordinal)))
			return null;

		return route;
	}

	// The fallback. It is a path rather than a plain index so that two unnamed buttons in different parts of
	// the same page do not collide, and it is prefixed so that a reader can see at a glance that this address
	// was derived rather than declared.
	private static string BuildPath(Element element, Element scopeRoot)
	{
		var path = new StringBuilder();

		for (var current = element; current is not null && !ReferenceEquals(current, scopeRoot); current = Parent(current))
		{
			var parent = Parent(current);
			var index = 0;

			if (parent is IVisualTreeElement visual)
			{
				index = visual
					.GetVisualChildren()
					.TakeWhile(child => !ReferenceEquals(child, current))
					.Count(child => child.GetType() == current.GetType());
			}

			path.Insert(0, $"/{current.GetType().Name}[{index}]");
		}

		return path.Length == 0 ? "~root" : "~" + path;
	}
}
