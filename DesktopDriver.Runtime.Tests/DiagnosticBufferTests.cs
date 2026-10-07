namespace StockSharp.DesktopDriver.Tests;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// The bounded log, and what it admits about its own limits.
/// </summary>
[TestClass]
public class DiagnosticBufferTests : BaseTestClass
{
	[TestMethod]
	public void EntriesComeBackInOrderAndTheCursorContinues()
	{
		var buffer = new UiDiagnosticBuffer();

		for (var index = 0; index < 5; index++)
			buffer.Append("info", "test", $"entry {index}");

		var first = buffer.Read(new UiDiagnosticQuery(null, 2, null, null));

		AreEqual(2, first.Items.Length);
		IsTrue(first.Truncated);

		var second = buffer.Read(new UiDiagnosticQuery(first.NextCursor, 10, null, null));

		AreEqual(3, second.Items.Length);
		AreEqual("entry 2", second.Items[0].Message);
		IsFalse(second.Truncated);
	}

	[TestMethod]
	public void ALogThatLostItsBeginningSaysSo()
	{
		// A gap nobody mentions reads as a period when nothing happened.
		var buffer = new UiDiagnosticBuffer(capacity: 3);

		for (var index = 0; index < 10; index++)
			buffer.Append("info", "test", $"entry {index}");

		var page = buffer.Read(new UiDiagnosticQuery("1", 10, null, null));

		IsTrue(page.HistoryLost);
		AreEqual(3, page.Items.Length);
	}

	[TestMethod]
	public void EntriesCanBeAskedForByWhatTheyAreAbout()
	{
		var buffer = new UiDiagnosticBuffer();
		var node = new Identity.UiNodeId("panel", "grid");

		buffer.Append("info", "test", "unrelated");
		buffer.Append("warning", "test", "about the grid", node);

		var page = buffer.Read(new UiDiagnosticQuery(null, 10, node, null));

		AreEqual(1, page.Items.Length);
		AreEqual("about the grid", page.Items[0].Message);
	}
}
