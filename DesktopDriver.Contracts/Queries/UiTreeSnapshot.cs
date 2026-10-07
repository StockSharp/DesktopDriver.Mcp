namespace StockSharp.DesktopDriver.Queries;

using System;
using System.Collections.Immutable;

using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// The tree as one reply: every node once, and the edges between them.
/// </summary>
/// <param name="InstanceId">The application instance.</param>
/// <param name="SnapshotId">This particular walk.</param>
/// <param name="Nodes">The nodes.</param>
/// <param name="Links">The edges.</param>
/// <param name="Truncated">Whether a limit stopped the walk short.</param>
/// <param name="Warnings">What the walk wants the reader to know, such as a cycle it cut.</param>
/// <remarks>
/// The reply is not claimed to be one instant of the whole application: each node carries its own stamp,
/// and that is what a reader must compare, rather than assuming the tree was frozen while it was built.
/// </remarks>
public sealed record UiTreeSnapshot(
	Guid InstanceId,
	Guid SnapshotId,
	ImmutableArray<UiNodeSnapshot> Nodes,
	ImmutableArray<UiChildLink> Links,
	bool Truncated,
	ImmutableArray<string> Warnings);
