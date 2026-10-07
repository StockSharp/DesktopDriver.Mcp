namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Collections.Immutable;
using System.Text.Json;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Serialization;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// The shapes a snapshot is written in.
/// </summary>
[TestClass]
public class SnapshotSchemaTests : BaseTestClass
{
	[TestMethod]
	public void AGridStateComesBackAsAGridState()
	{
		var state = new GridState(
			new GridCounts(
				UiField<long>.Known(4),
				UiField<long>.Known(4),
				UiField<long>.Known(4),
				UiField<long>.Known(4),
				UiField<long>.Unavailable(UiUnavailableReasons.NotCreated, null)),
			UiField<long>.Known(2),
			[new GridSort("Price", UiSortDirections.Descending, 0)],
			[],
			UiField<UiFilter>.Known(UiFilterGroup.Empty),
			UiSelectionSummary.Empty,
			UiField<GridCellRef>.Known(null),
			UiField<GridEditState>.Unavailable(UiUnavailableReasons.Unsupported, null));

		// Compared as the wire form rather than as records: a record holding an ImmutableArray does not
		// compare its contents, so record equality would pass on two snapshots with different rows.
		var json = UiJson.Write<UiState>(state);
		var restored = UiJson.Read<UiState>(json);

		AreEqual(json, UiJson.Write(restored));
		AreEqual(UiStateKinds.Grid, restored.Kind);
		AreEqual(UiSortDirections.Descending, ((GridState)restored).Sorts[0].Direction);
		AreEqual("Price", ((GridState)restored).Sorts[0].ColumnId);
	}

	[TestMethod]
	public void AStateShapeThisVersionDoesNotKnowIsRefused()
	{
		Throws<JsonException>(() => UiJson.Read<UiState>("{\"kind\":\"someFutureState\"}"));
	}

	[TestMethod]
	public void ANodeSnapshotCarriesItsStampAndItsState()
	{
		var snapshot = new UiNodeSnapshot(
			new UiNodeRef(new UiNodeId("panel:orders", "orders-grid"), null, "grid"),
			new UiSnapshotStamp(
				UiJson.SchemaVersion,
				Guid.Empty,
				Guid.Empty,
				new DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc),
				new UiRevisions(
					new UiRevision(Guid.Empty, 1),
					new UiRevision(Guid.Empty, 2),
					new UiRevision(Guid.Empty, 3)),
				UiConsistencies.VersionChecked),
			new UiPresentation(
				UiContentStatuses.Created,
				UiField<string>.Known("window:main"),
				UiField<bool>.Known(true),
				UiField<bool>.Known(true),
				UiField<bool>.Known(true),
				UiField<bool>.Known(true),
				UiField<bool>.Known(false),
				UiField<UiRect>.Known(new UiRect(0, 0, 100, 40)),
				UiField<double>.Known(1.5)),
			UiReadyStatuses.Ready,
			["grid.rows"],
			new BasicState(
				UiField<string>.Known("Orders"),
				UiField<UiValue>.Known(UiNullValue.Instance),
				UiField<bool>.Known(false),
				UiField<bool?>.Unavailable(UiUnavailableReasons.Unsupported, null),
				UiField<bool>.Known(true),
				UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, null),
				UiField<UiNodeId>.Known(null),
				UiField<UiNodeId>.Unavailable(UiUnavailableReasons.Unsupported, null),
				ImmutableArray<string>.Empty),
			UiCompleteness.Complete,
			ImmutableArray<string>.Empty);

		var json = UiJson.Write(snapshot);
		var restored = UiJson.Read<UiNodeSnapshot>(json);

		AreEqual(json, UiJson.Write(restored));
		AreEqual(snapshot.Node.Id, restored.Node.Id);
		AreEqual(UiContentStatuses.Created, restored.Presentation.ContentStatus);
		AreEqual("grid.rows", restored.Capabilities[0]);
	}

	[TestMethod]
	public void ARevisionOnlyMovesForwardInsideItsOwnEpoch()
	{
		var epoch = Guid.NewGuid();
		var baseline = new UiRevision(epoch, 1);

		IsTrue(new UiRevision(epoch, 2).IsAfter(baseline));
		IsFalse(new UiRevision(epoch, 1).IsAfter(baseline));
		IsFalse(new UiRevision(Guid.NewGuid(), 99).IsAfter(baseline),
			"A different source is not a later version of this one.");
	}
}
