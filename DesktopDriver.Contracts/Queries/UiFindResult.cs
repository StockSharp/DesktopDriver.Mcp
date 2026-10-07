namespace StockSharp.DesktopDriver.Queries;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Identity;

/// <summary>
/// What a search found.
/// </summary>
/// <param name="Items">The nodes.</param>
/// <param name="Truncated">Whether a limit stopped the search short.</param>
/// <param name="NextCursor">Where to continue, when there is more.</param>
/// <remarks>
/// Several matches are several matches. A helper that wants exactly one says so and fails on ambiguity,
/// because taking the first of them makes a test that passes for a reason nobody chose.
/// </remarks>
public sealed record UiFindResult(
	ImmutableArray<UiNodeRef> Items,
	bool Truncated,
	string NextCursor);
