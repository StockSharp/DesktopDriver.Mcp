namespace StockSharp.DesktopDriver.Tests;

using System;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Queries;

/// <summary>
/// Continuing a page that was cut short.
/// </summary>
/// <remarks>
/// A page that says it was truncated and hands back nothing to continue with is a dead end: the caller
/// has part of a list and no way to ask for the rest. Asking again with a bigger limit is not the same
/// answer - the thing being read may have moved, and the second reply would overlap the first silently.
/// </remarks>
[TestClass]
public class PageCursorTests : BaseTestClass
{
	private const string _rows = "grid.rows";
	private const string _request = "mode=viewData;columns=price,volume";

	[TestMethod]
	public void WithoutACursorAReadStartsWhereItWasTold()
		=> AreEqual(7L, UiPageCursor.StartAt(null, _rows, _request, 7));

	[TestMethod]
	public void ACursorSaysWhereTheNextPageBegins()
	{
		var cursor = UiPageCursor.Create(_rows, _request, 50);

		AreEqual(50L, UiPageCursor.StartAt(cursor, _rows, _request, 0));
	}

	[TestMethod]
	public void ACursorFromAnotherOperationIsRefused()
	{
		var cursor = UiPageCursor.Create("chart.points", _request, 50);

		Throws<ArgumentException>(() => UiPageCursor.StartAt(cursor, _rows, _request, 0));
	}

	[TestMethod]
	public void ACursorFromADifferentRequestIsRefused()
	{
		// The case worth refusing: the caller sorted the table between pages. Honouring the cursor would
		// join rows from two different orders into one answer, and nothing in it would show that.
		var cursor = UiPageCursor.Create(_rows, "mode=viewData;sort=price", 50);

		Throws<ArgumentException>(() => UiPageCursor.StartAt(cursor, _rows, _request, 0));
	}

	[TestMethod]
	public void SomethingThatIsNotACursorIsRefused()
	{
		Throws<ArgumentException>(() => UiPageCursor.StartAt("50", _rows, _request, 0));
		Throws<ArgumentException>(() => UiPageCursor.StartAt("bm90IGEgY3Vyc29y", _rows, _request, 0));
	}

	[TestMethod]
	public void ACursorReadsTheSameInAnyProcess()
	{
		// A caller may reconnect between pages, so the fingerprint cannot be the framework's own string
		// hash - that one is randomised per process and the cursor would stop being accepted.
		AreEqual(
			UiPageCursor.Create(_rows, _request, 50),
			UiPageCursor.Create(_rows, _request, 50));
	}
}
