namespace StockSharp.DesktopDriver.Queries;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Which blocks of a diagram to read.
/// </summary>
/// <param name="Keys">The blocks wanted, or empty for all of them.</param>
/// <param name="Page">Which page.</param>
/// <param name="Guard">What the caller believes it is reading.</param>
public sealed record DiagramNodesQuery(
	ImmutableArray<string> Keys,
	UiPageRequest Page,
	UiReadGuard Guard);
