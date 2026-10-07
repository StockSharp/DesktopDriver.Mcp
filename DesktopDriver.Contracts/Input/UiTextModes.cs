namespace StockSharp.DesktopDriver.Input;

/// <summary>
/// What typing does to the text that is already there.
/// </summary>
public enum UiTextModes
{
	/// <summary>Types after it.</summary>
	Append,

	/// <summary>Selects it first, the way a person would, and types over it.</summary>
	Replace,
}
