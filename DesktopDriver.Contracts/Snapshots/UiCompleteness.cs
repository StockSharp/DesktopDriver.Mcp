namespace StockSharp.DesktopDriver.Snapshots;

using System.Collections.Immutable;

/// <summary>
/// Whether anything was left out of a snapshot, and what.
/// </summary>
/// <param name="IsPartial">Whether something was left out.</param>
/// <param name="OmittedPaths">The schema paths that were not filled in.</param>
/// <remarks>
/// A partial answer is useful; a partial answer that looks complete is worse than no answer, because the
/// test that reads it concludes the missing part is absent rather than unread.
/// </remarks>
public sealed record UiCompleteness(bool IsPartial, ImmutableArray<string> OmittedPaths)
{
	/// <summary>
	/// Nothing was left out.
	/// </summary>
	public static UiCompleteness Complete { get; } = new(false, ImmutableArray<string>.Empty);
}
