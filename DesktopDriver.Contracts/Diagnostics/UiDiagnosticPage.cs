namespace StockSharp.DesktopDriver.Diagnostics;

using System.Collections.Immutable;

/// <summary>
/// One page of the diagnostic log.
/// </summary>
/// <param name="Items">The entries.</param>
/// <param name="NextCursor">Where to continue.</param>
/// <param name="HistoryLost">Whether older entries were dropped before the caller read them.</param>
/// <param name="Truncated">Whether a limit stopped the page short.</param>
/// <remarks>
/// The log is bounded, so it can lose its own beginning. Saying so is the point: a test that read a gap
/// without being told would conclude nothing happened there.
/// </remarks>
public sealed record UiDiagnosticPage(
	ImmutableArray<UiDiagnosticEntry> Items,
	string NextCursor,
	bool HistoryLost,
	bool Truncated);
