namespace StockSharp.DesktopDriver.Tests.Cli;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Cli;

/// <summary>
/// What each operation needs before anything is connected.
/// </summary>
/// <remarks>
/// Planned without an application, because none of this is about an application: a misspelt option is
/// the caller's mistake whether or not anything is running, and finding out after a window has been
/// clicked would be finding out too late.
/// </remarks>
[TestClass]
public class CommandTests : BaseTestClass
{
	[TestMethod]
	public void AnOperationNobodyWroteIsRefused()
	{
		var error = Throws<UiUsageException>(() => UiCommands.Plan("grid-rowz", UiArguments.Parse([])));

		IsTrue(error.Message.Contains("grid-rowz"), error.Message);
	}

	[TestMethod]
	public void EveryOperationTheProtocolHasCanBeAskedFor()
	{
		string[] named =
		[
			"session", "windows", "find", "tree", "snapshot",
			"grid-columns", "grid-rows", "grid-groups",
			"chart-series", "chart-points", "book-levels", "property-items", "dock-layout",
			"tree-items", "document-content", "diagram-nodes", "diagram-connections",
			"click", "type", "key", "scroll", "drag", "hover", "leave", "action-status", "wait",
			"screenshot", "artifact", "diagnostics",
		];

		foreach (var name in named)
			IsNotNull(UiCommands.Plan(name, UiArguments.Parse(Enough(name))), name);
	}

	[TestMethod]
	public void ReadingSomethingNeedsToKnowWhatToRead()
	{
		Throws<UiUsageException>(() => UiCommands.Plan("grid-rows", UiArguments.Parse([])));
		Throws<UiUsageException>(() => UiCommands.Plan("chart-points", UiArguments.Parse(["--node=w/1"])));
	}

	[TestMethod]
	public void LookingForNothingInParticularIsRefused()
	{
		// An empty selector matches the first node the search passes, which is never what was meant.
		Throws<UiUsageException>(() => UiCommands.Plan("find", UiArguments.Parse([])));
	}

	[TestMethod]
	public void ANodeIsAScopeAndAnIdentifier()
	{
		Throws<UiUsageException>(() => UiCommands.Plan("snapshot", UiArguments.Parse(["--node=justaname"])));
		Throws<UiUsageException>(() => UiCommands.Plan("snapshot", UiArguments.Parse(["--node=/1"])));
		IsNotNull(UiCommands.Plan("snapshot", UiArguments.Parse(["--node=window.main/grid.orders"])));
	}

	[TestMethod]
	public void WhatAPictureFramesIsNamedAsANodeIs()
	{
		var error = Throws<UiUsageException>(() => UiCommands.Plan("screenshot", UiArguments.Parse(["--node=w/1", "--highlight=justaname"])));

		IsTrue(error.Message.Contains("--highlight"), error.Message);
		IsNotNull(UiCommands.Plan("screenshot", UiArguments.Parse(["--node=w/1", "--highlight=w/2,w/grid#price"])));
	}

	[TestMethod]
	public void TheWholeApplicationIsAValidTreeToAskFor()
	{
		IsNotNull(UiCommands.Plan("tree", UiArguments.Parse([])));
	}

	[TestMethod]
	public void ScrollingNowhereIsRefused()
	{
		Throws<UiUsageException>(() => UiCommands.Plan("scroll", UiArguments.Parse(["--node=w/1"])));
		IsNotNull(UiCommands.Plan("scroll", UiArguments.Parse(["--node=w/1", "--dy=-3"])));
	}

	[TestMethod]
	public void ThePaneWhoseCrossIsPressedIsNamed()
	{
		Throws<UiUsageException>(() => UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=chartAreaClose"])));
		IsNotNull(UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=chartAreaClose", "--area=Volume"])));
	}

	[TestMethod]
	public void ThePointerIsBroughtOntoANodeAndTakenOffItsWindow()
	{
		Throws<UiUsageException>(() => UiCommands.Plan("hover", UiArguments.Parse([])));
		Throws<UiUsageException>(() => UiCommands.Plan("leave", UiArguments.Parse([])));
		IsNotNull(UiCommands.Plan("hover", UiArguments.Parse(["--node=w/1", "--part=chartPrice", "--area=Area0", "--price=70000"])));
		IsNotNull(UiCommands.Plan("leave", UiArguments.Parse(["--node=window:Main/Main"])));
	}

	[TestMethod]
	public void APartOfAGridNeedsToSayWhichPart()
	{
		Throws<UiUsageException>(() =>
			UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=gridCell", "--row=row-0"])));

		IsNotNull(UiCommands.Plan(
			"click",
			UiArguments.Parse(["--node=w/1", "--part=gridCell", "--row=row-0", "--column=price"])));
	}

	[TestMethod]
	public void AControlInsideACellHasToBeNamed()
	{
		Throws<UiUsageException>(() => UiCommands.Plan(
			"click",
			UiArguments.Parse(["--node=w/1", "--part=gridCellControl", "--row=row-0", "--column=security"])));

		IsNotNull(UiCommands.Plan(
			"click",
			UiArguments.Parse(["--node=w/1", "--part=gridCellControl", "--row=row-0", "--column=security", "--control=PickerButton"])));
	}

	[TestMethod]
	public void APropertyHasToBeNamedByItsPath()
	{
		Throws<UiUsageException>(() => UiCommands.Plan(
			"click",
			UiArguments.Parse(["--node=w/1", "--part=propertyValue"])));

		IsNotNull(UiCommands.Plan(
			"click",
			UiArguments.Parse(["--node=w/1", "--part=propertyValue", "--path=IsFixServer"])));
	}

	[TestMethod]
	public void AnItemOfAListIsNamedByItsPosition()
	{
		Throws<UiUsageException>(() => UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=listItem"])));

		IsNotNull(UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=listItem", "--index=5"])));
	}

	[TestMethod]
	public void AnEntryOfARibbonIsNamedByWhatTheMarkupCallsIt()
	{
		Throws<UiUsageException>(() => UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=ribbonItem"])));

		IsNotNull(UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=ribbonItem", "--item=ConnectButton"])));
	}

	[TestMethod]
	public void AnItemOfATreeIsNamedByItsPath()
	{
		Throws<UiUsageException>(() => UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=treeItem"])));
		Throws<UiUsageException>(() => UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=treeItemControl", "--item=Sources/Server"])));

		IsNotNull(UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=treeItem", "--item=Sources/Server"])));
		IsNotNull(UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=treeItemControl", "--item=Sources/Server", "--control=EnabledSwitch"])));
	}

	[TestMethod]
	public void AClickHoldsTheKeysItIsGivenAndNoOthers()
	{
		IsNotNull(UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--modifiers=control,shift"])));

		Throws<UiUsageException>(() => UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--modifiers=hyper"])));
	}

	[TestMethod]
	public void APriceOnAChartIsNamedByItsAreaAndThePrice()
	{
		Throws<UiUsageException>(() => UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=chartPrice", "--area=Area0"])));
		Throws<UiUsageException>(() => UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=chartPrice", "--price=70000"])));

		// A price is written the same way whatever the machine's language: a comma would be a thousands separator to one
		// reader and the decimal point to another.
		Throws<UiUsageException>(() => UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=chartPrice", "--area=Area0", "--price=70000,5"])));

		IsNotNull(UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=chartPrice", "--area=Area0", "--price=70000.5", "--modifiers=control"])));
	}

	[TestMethod]
	public void AnOrderOnAChartIsNamedByWhatTheChartCallsIt()
	{
		Throws<UiUsageException>(() => UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=chartOrderCancel"])));

		IsNotNull(UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=chartOrderCancel", "--order=1234567"])));
		IsNotNull(UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=chartOrder", "--order=1234567"])));
	}

	[TestMethod]
	public void ADragSaysWhereItIsLetGo()
	{
		Throws<UiUsageException>(() => UiCommands.Plan("drag", UiArguments.Parse(["--node=w/1", "--part=chartOrder", "--order=1234567"])));
		Throws<UiUsageException>(() => UiCommands.Plan("drag", UiArguments.Parse(["--node=w/1", "--part=chartOrder", "--order=1234567", "--to-part=chartPrice", "--to-area=Area0"])));

		IsNotNull(UiCommands.Plan("drag", UiArguments.Parse(
			["--node=w/1", "--part=chartOrder", "--order=1234567", "--to-part=chartPrice", "--to-area=Area0", "--to-price=70000"])));
	}

	[TestMethod]
	public void APartNobodyDefinedIsRefused()
	{
		Throws<UiUsageException>(() =>
			UiCommands.Plan("click", UiArguments.Parse(["--node=w/1", "--part=elbow"])));
	}

	[TestMethod]
	public void WaitingNeedsSomethingToWaitFor()
	{
		Throws<UiUsageException>(() => UiCommands.Plan("wait", UiArguments.Parse(["--node=w/1"])));

		IsNotNull(UiCommands.Plan("wait", UiArguments.Parse(["--node=w/1", "--exists"])));
		IsNotNull(UiCommands.Plan(
			"wait",
			UiArguments.Parse(["--node=w/1", "--field=rowCount", "--value=3", "--value-kind=int64"])));
	}

	[TestMethod]
	public void WaitingCannotAskTwoThingsAtOnce()
	{
		Throws<UiUsageException>(() =>
			UiCommands.Plan("wait", UiArguments.Parse(["--node=w/1", "--exists", "--missing"])));

		Throws<UiUsageException>(() =>
			UiCommands.Plan("wait", UiArguments.Parse(["--node=w/1", "--exists", "--field=x", "--value=1"])));
	}

	[TestMethod]
	public void AValueSaysWhatKindItIs()
	{
		// "100.5" is a price to one caller and a label to another, and guessing gets it wrong silently.
		Throws<UiUsageException>(() => UiCommands.Plan(
			"wait",
			UiArguments.Parse(["--node=w/1", "--field=price", "--value=abc", "--value-kind=decimal"])));

		IsNotNull(UiCommands.Plan(
			"wait",
			UiArguments.Parse(["--node=w/1", "--field=price", "--value=100.5", "--value-kind=decimal"])));
	}

	[TestMethod]
	public void SavingAnArtifactNeedsBothEnds()
	{
		Throws<UiUsageException>(() => UiCommands.Plan("artifact", UiArguments.Parse(["--id=a1"])));
		Throws<UiUsageException>(() => UiCommands.Plan("artifact", UiArguments.Parse(["--out=a.png"])));
		IsNotNull(UiCommands.Plan("artifact", UiArguments.Parse(["--id=a1", "--out=a.png"])));
	}

	[TestMethod]
	public void AnActionIdentifierIsAnIdentifier()
	{
		Throws<UiUsageException>(() => UiCommands.Plan("action-status", UiArguments.Parse(["--action=yesterday"])));
		IsNotNull(UiCommands.Plan(
			"action-status",
			UiArguments.Parse(["--action=4a5f2d7c8b914e0d9a6c3f1b2e7d8a05"])));
	}

	private static string[] Enough(string name)
		=> name switch
		{
			"find" => ["--name=Orders"],
			"chart-points" => ["--node=w/1", "--series=area/close"],
			"type" => ["--node=w/1", "--text=abc"],
			"key" => ["--node=w/1", "--key=Enter"],
			"scroll" => ["--node=w/1", "--dy=3"],
			"drag" => ["--node=w/1", "--part=chartOrder", "--order=1234567", "--to-part=chartPrice", "--to-area=Area0", "--to-price=70000"],
			"action-status" => ["--action=4a5f2d7c8b914e0d9a6c3f1b2e7d8a05"],
			"wait" => ["--node=w/1", "--exists"],
			"artifact" => ["--id=a1", "--out=a.png"],
			"session" or "windows" or "tree" or "diagnostics" => [],
			_ => ["--node=w/1"],
		};
}
