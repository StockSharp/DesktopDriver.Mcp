namespace StockSharp.DesktopDriver.Queries;

/// <summary>
/// Which page of a bounded read is wanted.
/// </summary>
/// <param name="Limit">How many items.</param>
/// <param name="Cursor">Where to continue from; absent for the first page.</param>
/// <remarks>
/// A cursor belongs to one target, one query and one revision of what it was reading. Continuing with it
/// after the grid was re-sorted is refused, because the two pages would be halves of two different orders.
/// </remarks>
public sealed record UiPageRequest(int Limit, string Cursor)
{
	/// <summary>
	/// The first page of the default size.
	/// </summary>
	public static UiPageRequest Default { get; } = new(50, null);
}
