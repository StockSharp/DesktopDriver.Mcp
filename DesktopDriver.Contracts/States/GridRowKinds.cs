namespace StockSharp.DesktopDriver.States;

/// <summary>
/// What a row in a grid represents.
/// </summary>
/// <remarks>
/// Group headers and totals are rows on screen but not records. Counting them as data is how a test ends
/// up asserting that a filter returned eleven of ten matches.
/// </remarks>
public enum GridRowKinds
{
	/// <summary>A record.</summary>
	Data,

	/// <summary>The header of a group.</summary>
	GroupHeader,

	/// <summary>A total or another aggregate row.</summary>
	Summary,

	/// <summary>A row standing in for data that has not arrived.</summary>
	Placeholder,
}
