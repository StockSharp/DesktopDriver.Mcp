namespace StockSharp.DesktopDriver.Identity;

/// <summary>
/// What a request is about: exactly one of an address or a live handle.
/// </summary>
/// <param name="Id">The meaning-level address.</param>
/// <param name="Handle">The live visual.</param>
/// <remarks>
/// Both at once is refused rather than resolved by precedence: the two can disagree, and a request that
/// carries a disagreement is a mistake in the caller worth reporting.
/// </remarks>
public sealed record UiTarget(UiNodeId Id, UiHandle Handle)
{
	/// <summary>
	/// Whether exactly one of the two ways to name a node is filled in.
	/// </summary>
	public bool IsValid => (Id is null) != (Handle is null);

	/// <summary>
	/// Names a node by its address.
	/// </summary>
	/// <param name="id">The address.</param>
	/// <returns>The target.</returns>
	public static UiTarget FromId(UiNodeId id) => new(id, null);

	/// <summary>
	/// Names a node by its live visual.
	/// </summary>
	/// <param name="handle">The handle.</param>
	/// <returns>The target.</returns>
	public static UiTarget FromHandle(UiHandle handle) => new(null, handle);
}
