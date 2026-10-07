namespace StockSharp.DesktopDriver.States;

/// <summary>
/// How a filter compares a field with its arguments.
/// </summary>
public enum UiFilterOperators
{
	/// <summary>Equal.</summary>
	Equal,

	/// <summary>Not equal.</summary>
	NotEqual,

	/// <summary>Less than.</summary>
	Less,

	/// <summary>Less than or equal.</summary>
	LessOrEqual,

	/// <summary>Greater than.</summary>
	Greater,

	/// <summary>Greater than or equal.</summary>
	GreaterOrEqual,

	/// <summary>Contains.</summary>
	Contains,

	/// <summary>Starts with.</summary>
	StartsWith,

	/// <summary>One of the arguments.</summary>
	In,

	/// <summary>Between the two arguments.</summary>
	Between,

	/// <summary>Has no value.</summary>
	IsNull,

	/// <summary>Has a value.</summary>
	IsNotNull,
}
