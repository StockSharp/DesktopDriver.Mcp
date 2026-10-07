namespace StockSharp.DesktopDriver.Snapshots;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.States;

/// <summary>
/// Everything one read returns about one node.
/// </summary>
/// <param name="Node">Which node this is.</param>
/// <param name="Stamp">When it was read and from what.</param>
/// <param name="Presentation">Where it is and whether it would take input.</param>
/// <param name="Ready">How ready it was.</param>
/// <param name="Capabilities">The operations this node supports.</param>
/// <param name="State">What it holds.</param>
/// <param name="Completeness">What was left out.</param>
/// <param name="Warnings">What the adapter wants the reader to know.</param>
/// <remarks>
/// Capabilities say what can be asked of the node, not that the node works. A grid that lists
/// <c>grid.rows</c> will answer that question; whether the rows are the right ones is the test's business.
/// </remarks>
public sealed record UiNodeSnapshot(
	UiNodeRef Node,
	UiSnapshotStamp Stamp,
	UiPresentation Presentation,
	UiReadyStatuses Ready,
	ImmutableArray<string> Capabilities,
	UiState State,
	UiCompleteness Completeness,
	ImmutableArray<string> Warnings);
