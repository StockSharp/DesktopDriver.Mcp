namespace StockSharp.DesktopDriver.Tests;

using System.Linq;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Serialization;

/// <summary>
/// What a request for a picture sent across the channel arrives as.
/// </summary>
[TestClass]
public class PictureRequestRoundTripTests : BaseTestClass
{
	[TestMethod]
	public void APictureArrivesWithWhatItFrames()
	{
		var request = new UiScreenshotRequest(
			UiTarget.FromId(new UiNodeId("window:MainWindow", "PART_Ribbon")),
			UiCaptureKinds.ControlRender,
			false,
			0,
			0,
			null,
			[new UiMask(new UiNodeId("window:MainWindow", "UserName"), null, UiMaskScopes.Text, null)],
			[
				new UiHighlight(new UiNodeId("window:MainWindow", "MarketDataButton"), null),
				new UiHighlight(new UiNodeId("panel:Orders", "OrderGrid"), new UiGridHeaderPart("Price")),
			]);

		var json = UiJson.Write(request);
		var restored = UiJson.Read<UiScreenshotRequest>(json);

		AreEqual(json, UiJson.Write(restored));
		AreEqual(2, restored.Highlight.Length);
		AreEqual("MarketDataButton", restored.Highlight[0].Node.LocalId);
		IsNull(restored.Highlight[0].Part);
		AreEqual("Price", ((UiGridHeaderPart)restored.Highlight[1].Part).ColumnId);
		AreEqual(1, restored.Mask.Length);
	}

	[TestMethod]
	public void APictureArrivesKeepingThePathsOutOfIt()
	{
		var request = new UiScreenshotRequest(
			UiTarget.FromId(new UiNodeId("window:MainWindow", "~root")),
			UiCaptureKinds.WindowCapture,
			false,
			0,
			0,
			null,
			[new UiMask(new UiNodeId("window:MainWindow", "~root"), null, UiMaskScopes.Paths, null)],
			[]);

		var json = UiJson.Write(request);
		var restored = UiJson.Read<UiScreenshotRequest>(json);

		AreEqual(json, UiJson.Write(restored));
		AreEqual(UiMaskScopes.Paths, restored.Mask.Single().Scope);
		IsNull(restored.Mask.Single().Part);
	}

	[TestMethod]
	public void APictureArrivesKeepingWhatAPatternMatchesOutOfIt()
	{
		var request = new UiScreenshotRequest(
			UiTarget.FromId(new UiNodeId("window:MainWindow", "LogsPage")),
			UiCaptureKinds.ControlRender,
			false,
			0,
			0,
			null,
			[new UiMask(new UiNodeId("window:MainWindow", "LogsPage"), null, UiMaskScopes.Pattern, @"(?<=\(id=)\d+")],
			[]);

		var json = UiJson.Write(request);
		var restored = UiJson.Read<UiScreenshotRequest>(json);

		AreEqual(json, UiJson.Write(restored));
		AreEqual(UiMaskScopes.Pattern, restored.Mask.Single().Scope);
		AreEqual(@"(?<=\(id=)\d+", restored.Mask.Single().Pattern);
	}

	[TestMethod]
	public void APictureFromAnOlderCallerFramesNothing()
	{
		// A caller built before frames existed sends no list of them, and its picture is taken as it always was.
		var restored = UiJson.Read<UiScreenshotRequest>(
			"""{"target":{"id":{"scopeId":"window:MainWindow","localId":"PART_Ribbon"}},"captureKind":"controlRender","includePopups":false,"maxPixelWidth":0,"maxPixelHeight":0,"guard":null,"mask":[]}""");

		IsTrue(restored.Highlight.IsDefaultOrEmpty);
	}
}
