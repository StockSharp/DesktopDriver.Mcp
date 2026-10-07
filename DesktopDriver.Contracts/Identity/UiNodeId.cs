namespace StockSharp.DesktopDriver.Identity;

/// <summary>
/// The meaning-level address of a node: what it is in the application, not which visual currently shows it.
/// </summary>
/// <param name="ScopeId">The panel, document or window the node belongs to.</param>
/// <param name="LocalId">The node's identifier inside that scope.</param>
/// <remarks>
/// A node keeps this address when its panel is moved to another dock group or floated out, and when the
/// visual behind it is recreated. That is what makes it usable in a test: the address survives everything
/// the user can do to the layout.
/// </remarks>
public sealed record UiNodeId(string ScopeId, string LocalId)
{
	/// <summary>
	/// Whether both parts are present, so the address can name a node.
	/// </summary>
	public bool IsComplete
		=> !string.IsNullOrEmpty(ScopeId) && !string.IsNullOrEmpty(LocalId);

	/// <summary>
	/// The address as one string, for logs and messages.
	/// </summary>
	/// <returns>Scope and local identifier separated by a slash.</returns>
	public override string ToString() => $"{ScopeId}/{LocalId}";
}
