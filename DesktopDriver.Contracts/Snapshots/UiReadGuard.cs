namespace StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// What the caller believed about a node when it asked, so a changed answer can be refused.
/// </summary>
/// <param name="ExpectedState">The state revision the caller saw.</param>
/// <param name="ExpectedView">The view revision the caller saw.</param>
/// <param name="ExpectedLayout">The layout revision the caller saw.</param>
/// <remarks>
/// This is what keeps paging honest. Reading page two of a grid that was re-sorted between the pages
/// would return a mixture of two orders that never existed on screen; the guard turns that into a refusal.
/// </remarks>
public sealed record UiReadGuard(
	UiRevision ExpectedState,
	UiRevision ExpectedView,
	UiRevision ExpectedLayout)
{
	/// <summary>
	/// A guard that expects nothing.
	/// </summary>
	public static UiReadGuard None { get; } = new(null, null, null);

	/// <summary>
	/// Whether the guard expects anything at all.
	/// </summary>
	public bool IsEmpty => ExpectedState is null && ExpectedView is null && ExpectedLayout is null;
}
