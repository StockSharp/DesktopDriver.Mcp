namespace StockSharp.DesktopDriver.Diagnostics;

using System;

using StockSharp.DesktopDriver.Identity;

/// <summary>
/// Which diagnostic entries to read.
/// </summary>
/// <param name="AfterCursor">Where to continue from; absent means the oldest kept entry.</param>
/// <param name="Limit">How many to return.</param>
/// <param name="RelatedNode">Only entries about one node.</param>
/// <param name="RelatedAction">Only entries about one action.</param>
public sealed record UiDiagnosticQuery(
	string AfterCursor,
	int Limit,
	UiNodeId RelatedNode,
	Guid? RelatedAction);
