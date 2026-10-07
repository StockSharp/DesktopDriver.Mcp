namespace StockSharp.DesktopDriver.Maui;

using Microsoft.Maui.Controls;

/// <summary>
/// Whether a person could use an element, as far as the elements above it decide.
/// </summary>
public static class MauiReach
{
	// Disabling a layout disables what is in it, and an entry of a shell is disabled the same way, so the
	// question is answered for the whole way up rather than for the element alone.
	/// <summary>
	/// Whether an element can be used: neither it nor anything it is in is disabled.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns><see langword="true"/> when it can.</returns>
	public static bool IsEnabled(Element element)
	{
		for (var current = element; current is not null; current = UiAutomationNames.Parent(current))
		{
			if (current is VisualElement { IsEnabled: false } or BaseShellItem { IsEnabled: false })
				return false;
		}

		return true;
	}

	/// <summary>
	/// Whether an element is shown as far as the elements are concerned: neither it nor anything it is in hidden.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns><see langword="true"/> when it is.</returns>
	public static bool IsVisible(Element element)
	{
		for (var current = element; current is not null; current = UiAutomationNames.Parent(current))
		{
			if (current is VisualElement { IsVisible: false } or BaseShellItem { IsVisible: false })
				return false;
		}

		return true;
	}

	// An element that lets the pointer through, or sits in a layout that passes that on to everything inside
	// it, is never what a click lands on.
	/// <summary>
	/// Whether a click can land on an element.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns><see langword="true"/> when it can.</returns>
	public static bool TakesInput(Element element)
	{
		if (element is VisualElement { InputTransparent: true })
			return false;

		for (var current = UiAutomationNames.Parent(element); current is not null; current = UiAutomationNames.Parent(current))
		{
			if (current is Layout { InputTransparent: true, CascadeInputTransparent: true })
				return false;
		}

		return true;
	}
}
