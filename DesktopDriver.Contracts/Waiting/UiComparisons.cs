namespace StockSharp.DesktopDriver.Waiting;

/// <summary>
/// How a waited-for field is compared with the expected value.
/// </summary>
public enum UiComparisons
{
	/// <summary>Equal.</summary>
	Equal,

	/// <summary>Not equal.</summary>
	NotEqual,

	/// <summary>Greater.</summary>
	Greater,

	/// <summary>Greater or equal.</summary>
	GreaterOrEqual,

	/// <summary>Less.</summary>
	Less,

	/// <summary>Less or equal.</summary>
	LessOrEqual,

	/// <summary>Contains.</summary>
	Contains,
}
