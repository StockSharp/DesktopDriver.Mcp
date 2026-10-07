namespace StockSharp.DesktopDriver.Adapters;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// What an adapter is told about the read it is taking part in.
/// </summary>
/// <param name="Node">Which node is being read.</param>
/// <param name="Revisions">Its revisions as the service read them.</param>
/// <param name="Options">How much was asked for.</param>
public sealed record UiCaptureContext(
	UiNodeRef Node,
	UiRevisions Revisions,
	UiCaptureOptions Options);
