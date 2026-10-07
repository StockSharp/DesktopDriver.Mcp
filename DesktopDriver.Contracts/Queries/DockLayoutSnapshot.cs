namespace StockSharp.DesktopDriver.Queries;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;

/// <summary>
/// A docking layout as one reply.
/// </summary>
/// <param name="Source">The workspace node it was read from.</param>
/// <param name="Stamp">When it was read and at what revision. The read pipeline fills this in.</param>
/// <param name="RootLayoutIds">The tops of the layout.</param>
/// <param name="Nodes">The groups and panels.</param>
/// <param name="Truncated">Whether a limit stopped the read short.</param>
/// <param name="Warnings">What the read wants the reader to know.</param>
/// <remarks>
/// Parents and children must agree: a reply whose panel claims a group that is not in it would read as a
/// tree with a hole. A subtree read says where it was cut instead of pretending the cut edge is the end.
/// </remarks>
public sealed record DockLayoutSnapshot(
	UiNodeRef Source,
	UiSnapshotStamp Stamp,
	ImmutableArray<string> RootLayoutIds,
	ImmutableArray<DockLayoutNode> Nodes,
	bool Truncated,
	ImmutableArray<string> Warnings) : IUiStamped
{
	/// <inheritdoc />
	public IUiStamped WithStamp(UiSnapshotStamp stamp) => this with { Stamp = stamp };
}
