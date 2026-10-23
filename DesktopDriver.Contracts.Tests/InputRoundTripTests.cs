namespace StockSharp.DesktopDriver.Tests;

using System;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Serialization;

/// <summary>
/// What an action sent across the channel arrives as.
/// </summary>
[TestClass]
public class InputRoundTripTests : BaseTestClass
{
	private static UiInputRequest Request(UiTargetPart part, UiInputAction action)
		=> new(Guid.NewGuid(), UiTarget.FromId(new UiNodeId("panel:ChartPanel", "Chart")), part, action, null, TimeSpan.FromSeconds(10));

	private static UiInputRequest RoundTrip(UiInputRequest request)
	{
		var json = UiJson.Write(request);
		var restored = UiJson.Read<UiInputRequest>(json);

		AreEqual(json, UiJson.Write(restored));

		return restored;
	}

	[TestMethod]
	public void AClickArrivesWithTheKeysHeldWhileItWasMade()
	{
		// Ctrl and a click on a chart place an order; the same click without it only moves the crosshair.
		var restored = RoundTrip(Request(
			new UiChartPricePart("Area0", 81234.5m),
			new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.Control | UiKeyModifiers.Shift)));

		var click = (UiClickAction)restored.Action;

		AreEqual(UiKeyModifiers.Control | UiKeyModifiers.Shift, click.Modifiers);
		AreEqual(UiPointerButtons.Left, click.Button);
		AreEqual(1, click.Count);
	}

	[TestMethod]
	public void APriceOnAChartArrivesAsThePriceThatWasSent()
	{
		var restored = RoundTrip(Request(
			new UiChartPricePart("Area0", 12345678901234.5678m),
			new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.None)));

		var part = (UiChartPricePart)restored.Part;

		AreEqual("Area0", part.AreaId);
		AreEqual(12345678901234.5678m, part.Price);
	}

	[TestMethod]
	public void ADragArrivesWithWhereItIsLetGo()
	{
		var restored = RoundTrip(Request(
			new UiChartOrderPart("1234567"),
			new UiDragAction(new UiChartPricePart("Area0", 70000m))));

		AreEqual("1234567", ((UiChartOrderPart)restored.Part).OrderId);

		var to = (UiChartPricePart)((UiDragAction)restored.Action).To;

		AreEqual("Area0", to.AreaId);
		AreEqual(70000m, to.Price);
	}

	[TestMethod]
	public void TheCrossThatClosesAPaneOfAChartArrivesNamingThePane()
	{
		// A pane added under the chart - the volume under the candles - is closed by the cross on its header.
		var restored = RoundTrip(Request(
			new UiChartAreaClosePart("Volume"),
			new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.None)));

		AreEqual("Volume", ((UiChartAreaClosePart)restored.Part).AreaId);
	}

	[TestMethod]
	public void APointerBroughtOntoATargetAndOneTakenOffItsWindowArriveAsThemselves()
	{
		var hovered = RoundTrip(Request(UiControlPart.Instance, UiHoverAction.Instance));
		var left = RoundTrip(Request(UiControlPart.Instance, UiLeaveAction.Instance));

		IsTrue(hovered.Action is UiHoverAction);
		AreEqual(UiHoverAction.KindName, hovered.Action.Kind);
		IsTrue(left.Action is UiLeaveAction);
		AreEqual(UiLeaveAction.KindName, left.Action.Kind);
	}

	[TestMethod]
	public void TheMarkThatCancelsAnOrderOnAChartIsAPartOfItsOwn()
	{
		var restored = RoundTrip(Request(
			new UiChartOrderCancelPart("1234567"),
			new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.None)));

		AreEqual("1234567", ((UiChartOrderCancelPart)restored.Part).OrderId);
		AreEqual(UiChartOrderCancelPart.KindName, restored.Part.Kind);
		AreNotEqual(UiChartOrderPart.KindName, restored.Part.Kind);
	}
}
