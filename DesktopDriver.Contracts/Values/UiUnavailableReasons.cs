namespace StockSharp.DesktopDriver.Values;

/// <summary>
/// Why a field carries no value.
/// </summary>
/// <remarks>
/// "No value" is not one thing. A control that has no such concept, a visual that has not been built yet
/// and a value deliberately withheld are three different answers, and a test written against them says
/// three different things.
/// </remarks>
public enum UiUnavailableReasons
{
	/// <summary>The reason itself could not be established.</summary>
	Unknown,

	/// <summary>The control has no such concept, so nothing will ever be read here.</summary>
	Unsupported,

	/// <summary>The visual does not exist yet, and reading is not allowed to create it.</summary>
	NotCreated,

	/// <summary>The content exists but its data has not arrived.</summary>
	NotLoaded,

	/// <summary>The value exists and was deliberately withheld by the read policy.</summary>
	Redacted,
}
