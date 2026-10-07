namespace StockSharp.DesktopDriver.Tests.Cli;

using System;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Cli;
using StockSharp.DesktopDriver.States;

/// <summary>
/// What the command line is read as, and what it refuses.
/// </summary>
[TestClass]
public class ArgumentsTests : BaseTestClass
{
	[TestMethod]
	public void AnOptionIsTheSameEitherWayRoundOfTheEquals()
	{
		AreEqual("abc", UiArguments.Parse(["--node", "abc"]).Optional("node"));
		AreEqual("abc", UiArguments.Parse(["--node=abc"]).Optional("node"));
	}

	[TestMethod]
	public void AValueThatLooksLikeAnOptionIsNotSwallowed()
	{
		var arguments = UiArguments.Parse(["--node", "--limit", "10"]);

		// --node was given nothing; taking --limit as its value would read the next option as the value
		// of the one before it and shift the whole line by one.
		IsNull(arguments.Optional("node"));
		AreEqual(10, arguments.Number("limit", 1));
	}

	[TestMethod]
	public void AnOptionGivenTwiceIsRefused()
	{
		// One of the two was meant, and there is no way to tell which.
		Throws<UiUsageException>(() => UiArguments.Parse(["--node=a", "--node=b"]));
	}

	[TestMethod]
	public void AnOptionNobodyAskedForIsRefused()
	{
		var arguments = UiArguments.Parse(["--node=a", "--limti=10"]);

		arguments.Optional("node");
		arguments.Number("limit", 50);

		var error = Throws<UiUsageException>(arguments.RefuseUnknown);

		IsTrue(error.Message.Contains("limti"), error.Message);
	}

	[TestMethod]
	public void AFlagIsAboutBeingThereRatherThanItsValue()
	{
		IsTrue(UiArguments.Parse(["--popups"]).Flag("popups"));
		IsFalse(UiArguments.Parse([]).Flag("popups"));
	}

	[TestMethod]
	public void ANumberThatIsNotANumberIsRefused()
	{
		Throws<UiUsageException>(() => UiArguments.Parse(["--limit=many"]).Number("limit", 1));
	}

	[TestMethod]
	public void AnIndexIsMissingRatherThanNought()
	{
		// Nought is a row, so a missing --start cannot answer nought without asking for the first row.
		IsNull(UiArguments.Parse([]).Index("start"));
		AreEqual(0L, UiArguments.Parse(["--start=0"]).Index("start").Value);
	}

	[TestMethod]
	public void AChoiceIsOneOfItsNames()
	{
		AreEqual(GridRowsModes.Viewport, UiArguments.Parse(["--mode=viewport"]).Choice("mode", GridRowsModes.ViewData));
		AreEqual(GridRowsModes.ViewData, UiArguments.Parse([]).Choice("mode", GridRowsModes.ViewData));
		Throws<UiUsageException>(() => UiArguments.Parse(["--mode=sideways"]).Choice("mode", GridRowsModes.ViewData));
	}

	[TestMethod]
	public void AChoiceIsNotANumberInDisguise()
	{
		// "1" parses as an enum in .NET whether or not anything is defined for it, and a caller who wrote
		// a number meant a name.
		Throws<UiUsageException>(() => UiArguments.Parse(["--mode=7"]).Choice("mode", GridRowsModes.ViewData));
	}

	[TestMethod]
	public void AListIsSplitAndTrimmed()
	{
		var items = UiArguments.Parse(["--columns= price , volume ,"]).List("columns");

		AreEqual(2, items.Length);
		AreEqual("price", items[0]);
		AreEqual("volume", items[1]);
	}

	[TestMethod]
	public void AMomentIsReadAsUtc()
	{
		var moment = UiArguments.Parse(["--from=2026-01-02T10:00:00Z"]).Moment("from");

		IsNotNull(moment);
		AreEqual(DateTimeKind.Utc, moment.Value.Kind);
		AreEqual(new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc), moment.Value);
	}

	[TestMethod]
	public void AMomentWithNoZoneIsUtcRatherThanLocal()
	{
		// Everything in this protocol is UTC. Reading a bare time as the machine's own zone would shift
		// the window by whatever the machine happens to be set to.
		var moment = UiArguments.Parse(["--from=2026-01-02T10:00:00"]).Moment("from");

		AreEqual(new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc), moment.Value);
	}

	[TestMethod]
	public void SomethingRequiredThatIsNotThereIsRefused()
	{
		Throws<UiUsageException>(() => UiArguments.Parse([]).Required("node"));
	}
}
