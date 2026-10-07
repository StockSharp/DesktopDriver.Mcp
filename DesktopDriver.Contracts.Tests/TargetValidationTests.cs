namespace StockSharp.DesktopDriver.Tests;

using System;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Identity;

/// <summary>
/// How a request names what it is about.
/// </summary>
[TestClass]
public class TargetValidationTests : BaseTestClass
{
	[TestMethod]
	public void ATargetNamesANodeExactlyOneWay()
	{
		var id = new UiNodeId("panel:orders", "orders-grid");
		var handle = new UiHandle(Guid.NewGuid(), Guid.NewGuid(), 1);

		IsTrue(UiTarget.FromId(id).IsValid);
		IsTrue(UiTarget.FromHandle(handle).IsValid);
		IsFalse(new UiTarget(id, handle).IsValid, "Two ways of naming can disagree.");
		IsFalse(new UiTarget(null, null).IsValid, "A target that names nothing is a mistake, not a wildcard.");
	}

	[TestMethod]
	public void AnAddressNeedsBothOfItsParts()
	{
		IsTrue(new UiNodeId("panel:orders", "orders-grid").IsComplete);
		IsFalse(new UiNodeId("", "orders-grid").IsComplete);
		IsFalse(new UiNodeId("panel:orders", "").IsComplete);
	}

	[TestMethod]
	public void AnEmptySelectorWouldMatchEverythingAndSaysSo()
	{
		IsTrue(UiSelector.Any.IsEmpty);
		IsFalse((UiSelector.Any with { AutomationId = "orders-grid" }).IsEmpty);
	}
}
