namespace StockSharp.DesktopDriver.Diagnostics;

using System;

using StockSharp.DesktopDriver.Identity;

/// <summary>
/// One thing the module noticed.
/// </summary>
/// <param name="Sequence">Its place in the session's log.</param>
/// <param name="Timestamp">When it happened, UTC.</param>
/// <param name="Severity">How much it matters.</param>
/// <param name="Code">What kind of thing it is.</param>
/// <param name="Message">What happened.</param>
/// <param name="RelatedNode">The node it is about, when it is about one.</param>
/// <param name="RelatedAction">The action it is about, when it is about one.</param>
public sealed record UiDiagnosticEntry(
	long Sequence,
	DateTime Timestamp,
	string Severity,
	string Code,
	string Message,
	UiNodeId RelatedNode,
	Guid? RelatedAction);
