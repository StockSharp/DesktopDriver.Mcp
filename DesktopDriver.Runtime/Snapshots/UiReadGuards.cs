namespace StockSharp.DesktopDriver.Runtime;

using System;

using StockSharp.DesktopDriver.Serialization;
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// What a caller said it was looking at, and whether it is still that.
/// </summary>
/// <remarks>
/// A guard is how a caller says "answer only if this is the state I already saw". Without it a runner
/// that sorted a table and then read its rows could be handed the rows from before the sort, and the
/// two would look like one consistent answer.
/// <para>
/// One implementation for every read. A second copy of this comparison would agree with the first
/// until the day one of them was changed, and the reads that disagreed would be the ones a test
/// believed.
/// </para>
/// </remarks>
public static class UiReadGuards
{
	/// <summary>
	/// Whether this guard asks for anything at all.
	/// </summary>
	/// <param name="guard">The guard, or <see langword="null"/>.</param>
	/// <returns><see langword="true"/> when it names at least one revision.</returns>
	public static bool IsStrict(UiReadGuard guard) => guard is not null && !guard.IsEmpty;

	/// <summary>
	/// Refuses the read when what the caller saw has moved on.
	/// </summary>
	/// <param name="guard">What the caller believes it is reading.</param>
	/// <param name="actual">What the node's revisions say now.</param>
	public static void Enforce(UiReadGuard guard, UiRevisions actual)
	{
		ArgumentNullException.ThrowIfNull(guard);
		ArgumentNullException.ThrowIfNull(actual);

		Check(guard.ExpectedState, actual.State, "state");
		Check(guard.ExpectedView, actual.View, "view");
		Check(guard.ExpectedLayout, actual.Layout, "layout");

		static void Check(UiRevision expected, UiRevision actual, string what)
		{
			if (expected is null)
				return;

			if (expected.Epoch != actual.Epoch)
				throw UiErrors.Changed($"The {what} of this node comes from a different source than the caller saw.");

			if (expected.Version != actual.Version)
				throw UiErrors.Changed($"The {what} of this node has moved on since the caller saw it.");
		}
	}

	/// <summary>
	/// The stamp a page carries, so that a caller can guard its next read with what this one saw.
	/// </summary>
	/// <param name="instanceId">The running copy that answered.</param>
	/// <param name="revisions">The revisions the page was read at.</param>
	/// <param name="strict">Whether the read was guarded.</param>
	/// <returns>The stamp.</returns>
	/// <remarks>
	/// A page without one cannot be continued safely: the caller has nothing to say "still the same" about,
	/// so the second page could come from a table that had been re-sorted in between.
	/// </remarks>
	public static UiSnapshotStamp Stamp(Guid instanceId, UiRevisions revisions, bool strict)
		=> new(
			UiJson.SchemaVersion,
			instanceId,
			Guid.NewGuid(),
			DateTime.UtcNow,
			revisions ?? new UiRevisions(null, null, null),
			strict ? UiConsistencies.VersionChecked : UiConsistencies.UiThreadRead);
}
