namespace StockSharp.DesktopDriver.Identity;

/// <summary>
/// What to look for when the address is not known yet.
/// </summary>
/// <param name="ScopeId">Restricts the search to one panel, document or window.</param>
/// <param name="AutomationId">The element's automation identifier.</param>
/// <param name="Name">The element's name.</param>
/// <param name="Kind">The node kind.</param>
/// <param name="Text">Displayed text, as a last resort.</param>
/// <param name="SurfaceId">Restricts the search to one window or popup.</param>
/// <remarks>
/// Every filled-in field narrows the search. Displayed text is the weakest of them: it is translated, so a
/// test that leans on it fails in the next language rather than when the interface actually changes.
/// </remarks>
public sealed record UiSelector(
	string ScopeId,
	string AutomationId,
	string Name,
	string Kind,
	string Text,
	string SurfaceId)
{
	/// <summary>
	/// A selector that matches everything, to be narrowed with <c>with</c> expressions.
	/// </summary>
	public static UiSelector Any { get; } = new(null, null, null, null, null, null);

	/// <summary>
	/// Whether the selector names nothing at all and would match the whole application.
	/// </summary>
	public bool IsEmpty
		=> string.IsNullOrEmpty(ScopeId) && string.IsNullOrEmpty(AutomationId) &&
			string.IsNullOrEmpty(Name) && string.IsNullOrEmpty(Kind) &&
			string.IsNullOrEmpty(Text) && string.IsNullOrEmpty(SurfaceId);
}
