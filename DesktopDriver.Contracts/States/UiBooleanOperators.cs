namespace StockSharp.DesktopDriver.States;

/// <summary>
/// How the parts of a composite filter are combined.
/// </summary>
public enum UiBooleanOperators
{
	/// <summary>Every part must match.</summary>
	All,

	/// <summary>At least one part must match.</summary>
	Any,
}
