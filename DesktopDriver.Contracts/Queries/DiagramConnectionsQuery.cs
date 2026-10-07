namespace StockSharp.DesktopDriver.Queries;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Which connections of a diagram to read.
/// </summary>
/// <param name="NodeKey">Only the ones touching this block; all of them when empty.</param>
/// <param name="Keys">The connections wanted, or empty for all of them.</param>
/// <param name="Page">Which page.</param>
/// <param name="Guard">What the caller believes it is reading.</param>
public sealed record DiagramConnectionsQuery(
	string NodeKey,
	ImmutableArray<string> Keys,
	UiPageRequest Page,
	UiReadGuard Guard);
