namespace StockSharp.DesktopDriver.Tests;

using System.Collections.Generic;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Waiting;

/// <summary>
/// What the version counters do, and what they must not do.
/// </summary>
[TestClass]
public class RevisionTests : BaseTestClass
{
	private static readonly UiNodeId _id = new("panel:orders", "orders-grid");

	[TestMethod]
	public void ReadingDoesNotCountAsAChange()
	{
		// If it did, two reads in a row would look like a change and the counter would answer nothing.
		var tracker = new UiRevisionTracker();

		var first = tracker.Read(_id);
		var second = tracker.Read(_id);

		AreEqual(first, second);
	}

	[TestMethod]
	public void OnlyTheCounterThatChangedMoves()
	{
		var tracker = new UiRevisionTracker();
		var before = tracker.Read(_id);

		tracker.Bump(_id, UiRevisionKinds.View);
		var after = tracker.Read(_id);

		AreEqual(before.State.Version, after.State.Version);
		AreEqual(before.Layout.Version, after.Layout.Version);
		AreEqual(before.View.Version + 1, after.View.Version);
	}

	[TestMethod]
	public void AReplacedSourceStartsCountingAgainUnderANewEpoch()
	{
		var tracker = new UiRevisionTracker();

		tracker.Bump(_id, UiRevisionKinds.View);
		tracker.Bump(_id, UiRevisionKinds.View);
		var before = tracker.Read(_id);

		tracker.ResetEpoch(_id);
		var after = tracker.Read(_id);

		AreNotEqual(before.View.Epoch, after.View.Epoch);
		AreEqual(0, after.View.Version);
		IsFalse(after.View.IsAfter(before.View), "A new source is not a later version of the old one.");
	}

	[TestMethod]
	public void AWatcherHearsAboutChangesUntilItStopsWatching()
	{
		var tracker = new UiRevisionTracker();
		var heard = new List<UiRevisions>();

		var subscription = tracker.Subscribe(_id, heard.Add);

		tracker.Bump(_id, UiRevisionKinds.State);
		tracker.Bump(_id, UiRevisionKinds.Layout);

		subscription.Dispose();
		tracker.Bump(_id, UiRevisionKinds.State);

		AreEqual(2, heard.Count);
	}
}
