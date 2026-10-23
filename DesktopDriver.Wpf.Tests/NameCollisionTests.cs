namespace StockSharp.DesktopDriver.Tests.Wpf;

using System.Windows;
using System.Windows.Controls;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Wpf;

/// <summary>
/// What a node is called when a control and a part inside it carry the same name.
/// </summary>
[TestClass]
public class NameCollisionTests : BaseTestClass
{
	/// <summary>
	/// A part named like the control it sits in is addressed through that control.
	/// </summary>
	/// <remarks>
	/// A control built from markup of its own can give one of its parts the name the window gave the control - the way
	/// an instrument picker the panel calls SecuritiesGrid calls its table SecuritiesGrid as well. The window's name
	/// stays the control's address; one address cannot mean the part too.
	/// </remarks>
	[STATestMethod]
	public void APartNamedLikeTheControlItSitsInIsAddressedThroughIt()
	{
		var inner = new ListBox { Name = "Rows" };
		var outer = new UserControl { Name = "Rows", Content = inner };

		var window = new Window
		{
			Name = "NestedWindow",
			Width = 320,
			Height = 240,
			Left = 80,
			Top = 80,
			WindowStartupLocation = WindowStartupLocation.Manual,
			ShowActivated = false,
			ShowInTaskbar = false,
			Content = outer,
		};

		window.Show();

		try
		{
			window.UpdateLayout();

			AreEqual(new UiNodeId("window:NestedWindow", "Rows"), UiAutomationNames.GetNodeId(outer));
			AreEqual(new UiNodeId("window:NestedWindow", "Rows.Rows"), UiAutomationNames.GetNodeId(inner));
		}
		finally
		{
			window.Close();
		}
	}
}
