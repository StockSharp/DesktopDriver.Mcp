namespace StockSharp.DesktopDriver.Tests;

using System.Linq;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Things that stand together are framed together; a frame never cuts through another.
/// </summary>
/// <remarks>
/// The tabs of a strip and the buttons of a toolbar stand edge to edge. Framed one by one, every frame cut through its
/// neighbours' and a page about them showed a chain of brackets instead of the strip it is about.
/// </remarks>
[TestClass]
public class FrameMergeTests : BaseTestClass
{
	// How far outside its place a frame's outer edge runs.
	private const double _reach = 6;

	[TestMethod]
	public void TabsOfOneStripAreOneFrameRoundTheStrip()
	{
		var places = UiFrames.Merged([new UiRect(0, 0, 40, 20), new UiRect(40, 0, 50, 20), new UiRect(90, 0, 30, 20)], _reach);

		AreEqual(new UiRect(0, 0, 120, 20), places.Single());
	}

	[TestMethod]
	public void ButtonsSideBySideAreOneFrameRoundThemAll()
	{
		// The commands of a toolbar are controls of their own, and their frames would cut through each other all the same.
		var places = UiFrames.Merged([new UiRect(0, 0, 70, 24), new UiRect(74, 0, 110, 24), new UiRect(188, 0, 100, 24)], _reach);

		AreEqual(new UiRect(0, 0, 288, 24), places.Single());
	}

	[TestMethod]
	public void FramesWithRoomBetweenThemKeepAFrameEach()
	{
		var places = UiFrames.Merged([new UiRect(0, 0, 40, 20), new UiRect(200, 0, 40, 20)], _reach);

		AreEqual(2, places.Count);
	}

	[TestMethod]
	public void AControlFramedInsideAFramedPanelKeepsItsOwnFrame()
	{
		// A page about a panel and one button of it frames both: the button's frame stands inside the panel's.
		var places = UiFrames.Merged([new UiRect(0, 0, 300, 200), new UiRect(20, 20, 60, 24)], _reach);

		AreEqual(2, places.Count);
	}

	[TestMethod]
	public void AFrameThatGrewReachesThePlaceItMissedBefore()
	{
		// The first and the last tab are apart until the one between them joins either.
		var places = UiFrames.Merged([new UiRect(0, 0, 40, 20), new UiRect(90, 0, 30, 20), new UiRect(40, 0, 50, 20)], _reach);

		AreEqual(new UiRect(0, 0, 120, 20), places.Single());
	}
}
