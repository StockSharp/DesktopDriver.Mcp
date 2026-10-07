namespace StockSharp.DesktopDriver.Avalonia;

using System;
using System.Linq;
using System.Text;

using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.VisualTree;

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
	/// The scope a control belongs to, set by whatever owns that part of the screen.
	/// </summary>
	/// <remarks>
	/// A dock panel, a dialog or a document sets this on its root so that everything inside it is
	/// addressed relative to it, and keeps its addresses when the panel is moved to another window.
	/// </remarks>
	public static readonly AttachedProperty<string> ScopeIdProperty =
		AvaloniaProperty.RegisterAttached<Control, string>("ScopeId", typeof(UiAutomationNames));

	/// <summary>
	/// Reads the scope set on a control.
	/// </summary>
	/// <param name="control">The control.</param>
	/// <returns>The scope, or <see langword="null"/>.</returns>
	public static string GetScopeId(Control control) => control?.GetValue(ScopeIdProperty);

	/// <summary>
	/// Declares that a control is the root of a scope.
	/// </summary>
	/// <param name="control">The control.</param>
	/// <param name="value">The scope identifier.</param>
	public static void SetScopeId(Control control, string value)
	{
		ArgumentNullException.ThrowIfNull(control);

		control.SetValue(ScopeIdProperty, value);
	}

	/// <summary>
	/// Works out the address of a control.
	/// </summary>
	/// <param name="control">The control.</param>
	/// <returns>Its address, or <see langword="null"/> when it is not attached to anything.</returns>
	public static UiNodeId GetNodeId(Control control)
	{
		if (control is null)
			return null;

		var scopeRoot = FindScopeRoot(control, out var scopeId);

		if (scopeId is null)
			return null;

		return new UiNodeId(scopeId, GetLocalId(control, scopeRoot));
	}

	/// <summary>
	/// Whether a control's address is one a test can rely on.
	/// </summary>
	/// <param name="control">The control.</param>
	/// <returns><see langword="true"/> when the address comes from a declared identifier or a name.</returns>
	public static bool HasStableId(Control control)
		=> control is not null &&
			(!string.IsNullOrEmpty(AutomationProperties.GetAutomationId(control)) ||
				!string.IsNullOrEmpty(control.Name));

	/// <summary>
	/// The identifier a popup surface was given by whatever opened it.
	/// </summary>
	/// <remarks>
	/// Every popup root is the same type, so the type name would make a menu and a tooltip open at the
	/// same moment into one surface. What tells them apart is what opened them, and only the side that
	/// found the popup knows that.
	/// </remarks>
	public static readonly AttachedProperty<string> SurfaceIdProperty =
		AvaloniaProperty.RegisterAttached<TopLevel, string>("SurfaceId", typeof(UiAutomationNames));

	/// <summary>
	/// Names a popup surface after whatever opened it.
	/// </summary>
	/// <param name="root">The popup root.</param>
	/// <param name="value">The surface identifier.</param>
	public static void SetSurfaceId(TopLevel root, string value)
	{
		ArgumentNullException.ThrowIfNull(root);

		root.SetValue(SurfaceIdProperty, value);
	}

	/// <summary>
	/// The identifier of a window or popup as a surface.
	/// </summary>
	/// <param name="root">The window or popup root.</param>
	/// <returns>Its surface identifier.</returns>
	public static string GetSurfaceId(TopLevel root)
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

		var declared = root.GetValue(SurfaceIdProperty);

		return string.IsNullOrEmpty(declared) ? $"popup:{root.GetType().Name}" : declared;
	}

	// A popup is a surface of its own and an ordinary walk stops at it, but a menu item belongs to the
	// window whose menu was opened rather than to a scope that lasts only while the menu is down. The
	// popup root keeps the control that opened it as its logical parent, and that control is back in the
	// window, so the walk carries on from there. Bounded, because a tree that led back into itself would
	// be a hang rather than a wrong answer.
	private static Control FindScopeRoot(Control control, out string scopeId)
	{
		for (var hop = 0; control is not null && hop < _popupDepth; hop++)
		{
			for (var current = control; current is not null; current = current.GetVisualParent() as Control)
			{
				var declared = GetScopeId(current) ?? UiAutomationScopes.Of(current);

				if (!string.IsNullOrEmpty(declared))
				{
					scopeId = declared;

					return current;
				}
			}

			var root = TopLevel.GetTopLevel(control);

			if (root is Window || root?.Parent is not Control opener)
			{
				scopeId = GetSurfaceId(root);

				return root;
			}

			// The popup of a context menu is in no window, so the walk carries on from the control the menu was
			// opened on instead.
			control = opener is Popup { PlacementTarget: { } target } && TopLevel.GetTopLevel(opener) is null
				? target
				: opener;
		}

		scopeId = null;

		return null;
	}

	private static string GetLocalId(Control control, Control scopeRoot)
	{
		var declared = AutomationProperties.GetAutomationId(control);

		if (!string.IsNullOrEmpty(declared))
			return declared;

		if (!string.IsNullOrEmpty(control.Name))
		{
			if (control.TemplatedParent is null)
				return control.Name;

			// A name a control template gave its own part - PART_ContentPresenter and the like - is carried by
			// every instance of that template, so on its own it is no address: a walk that had passed the first
			// would treat the second as somewhere it had been, and everything inside it would vanish from the tree.
			// It is unique within the control the template belongs to, so that control's address qualifies it.
			if (control.TemplatedParent is Control owner && GetLocalId(owner, scopeRoot) is { } ownerId && !ownerId.StartsWith('~'))
				return $"{ownerId}.{control.Name}";
		}

		return BuildPath(control, scopeRoot);
	}

	// The fallback. It is a path rather than a plain index so that two unnamed buttons in different
	// parts of the same panel do not collide, and it is prefixed so that a reader can see at a glance
	// that this address was derived rather than declared.
	private static string BuildPath(Control control, Control scopeRoot)
	{
		var path = new StringBuilder();

		for (var current = control; current is not null && current != scopeRoot; current = current.GetVisualParent() as Control)
		{
			var parent = current.GetVisualParent() as Control;
			var index = 0;

			if (parent is not null)
			{
				index = parent.GetVisualChildren()
					.OfType<Control>()
					.TakeWhile(child => !ReferenceEquals(child, current))
					.Count(child => child.GetType() == current.GetType());
			}

			path.Insert(0, $"/{current.GetType().Name}[{index}]");
		}

		return path.Length == 0 ? "~root" : "~" + path.ToString();
	}
}
