namespace StockSharp.DesktopDriver.Snapshots;

using System;

/// <summary>
/// When and from what a snapshot was taken.
/// </summary>
/// <param name="SchemaVersion">The protocol schema the snapshot was written against.</param>
/// <param name="InstanceId">The application instance it came from.</param>
/// <param name="SnapshotId">This particular capture.</param>
/// <param name="CapturedAt">The moment of capture, UTC.</param>
/// <param name="Revisions">What the node's versions were at that moment.</param>
/// <param name="Consistency">How the copy was taken.</param>
public sealed record UiSnapshotStamp(
	string SchemaVersion,
	Guid InstanceId,
	Guid SnapshotId,
	DateTime CapturedAt,
	UiRevisions Revisions,
	UiConsistencies Consistency);
