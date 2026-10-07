namespace StockSharp.DesktopDriver.Protocol;

/// <summary>
/// The protocol itself.
/// </summary>
public static class UiProtocol
{
	/// <summary>
	/// The version both sides of a connection have to agree on.
	/// </summary>
	/// <remarks>
	/// Only the part before the dot is compared. A change that adds an operation or a field leaves older
	/// callers working; a change that moves one does not, and that is what the first number is for.
	/// </remarks>
	public const string Version = "1.0";
}
