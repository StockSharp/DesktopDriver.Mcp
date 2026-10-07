namespace StockSharp.DesktopDriver.Maui;

using Microsoft.Maui.Controls;

using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// What a key does to a MAUI field.
/// </summary>
public static class MauiKeys
{
	/// <summary>
	/// Presses a key in a field.
	/// </summary>
	/// <remarks>
	/// Named by the physical key, as the protocol names keys. A field in MAUI answers the key that finishes it and
	/// nothing else a test could depend on, so that is the one key there is.
	/// </remarks>
	/// <param name="field">The field.</param>
	/// <param name="key">The key.</param>
	public static void Press(InputView field, string key)
	{
		if (key is not ("Enter" or "NumpadEnter"))
			throw UiErrors.Unsupported($"'{key}' is not a key a MAUI field answers; Enter is.");

		switch (field)
		{
			case Entry entry:
				entry.SendCompleted();
				break;

			case SearchBar search:
				search.OnSearchButtonPressed();
				break;

			// The platform field writes a new line as a carriage return, and so does a person pressing Enter.
			case Editor editor:
				editor.Text = (editor.Text ?? string.Empty) + "\r";
				break;

			default:
				throw UiErrors.Unsupported($"A {field.GetType().Name} does not answer Enter.");
		}
	}
}
