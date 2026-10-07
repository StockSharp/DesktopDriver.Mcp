namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Collections.Immutable;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Why the same click is never sent twice.
/// </summary>
[TestClass]
public class ActionJournalTests : BaseTestClass
{
	private static UiInputRequest Request(Guid id, string columnId = "Price")
		=> new(
			id,
			UiTarget.FromId(new UiNodeId("panel:orders", "orders-grid")),
			new UiGridHeaderPart(columnId),
			new UiClickAction(UiPointerButtons.Left, 1),
			null,
			TimeSpan.FromSeconds(10));

	private static UiActionReceipt Receipt(Guid id)
		=> new(id, UiActionStatuses.Dispatched, "test", UiField<Snapshots.UiRevisions>.Unavailable(UiUnavailableReasons.Unsupported, null),
			UiField<Snapshots.UiRevisions>.Unavailable(UiUnavailableReasons.Unsupported, null), true, null);

	[TestMethod]
	public void AskingAgainWithTheSameIdentifierReturnsWhatHappenedInsteadOfDoingItAgain()
	{
		// This is what makes a dropped connection safe: the caller retries the question, not the click.
		var journal = new UiActionJournal();
		var id = Guid.NewGuid();

		IsTrue(journal.TryClaim(Request(id), out _));
		journal.Complete(Receipt(id));

		IsFalse(journal.TryClaim(Request(id), out var existing));
		AreEqual(UiActionStatuses.Dispatched, existing.Status);
	}

	[TestMethod]
	public void ReusingAnIdentifierForADifferentActionIsRefused()
	{
		var journal = new UiActionJournal();
		var id = Guid.NewGuid();

		journal.TryClaim(Request(id, "Price"), out _);

		var error = Throws<UiAutomationException>(() => journal.TryClaim(Request(id, "Volume"), out _));

		AreEqual(UiErrorCodes.ActionConflict, error.Error.Code);
	}

	[TestMethod]
	public void ASessionRefusesNewActionsRatherThanForgettingOldOnes()
	{
		// A forgotten identifier could be replayed, which is the one thing the journal exists to prevent.
		var journal = new UiActionJournal(capacity: 2);

		journal.TryClaim(Request(Guid.NewGuid()), out _);
		journal.TryClaim(Request(Guid.NewGuid()), out _);

		var error = Throws<UiAutomationException>(() => journal.TryClaim(Request(Guid.NewGuid()), out _));

		AreEqual(UiErrorCodes.LimitExceeded, error.Error.Code);
	}

	[TestMethod]
	public void AnActionNobodyClaimedIsUnknown()
	{
		IsNull(new UiActionJournal().Find(Guid.NewGuid()));
	}
}
