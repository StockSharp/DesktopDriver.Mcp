namespace StockSharp.DesktopDriver.Tests.Maui;

using Ecng.UnitTesting;

using Microsoft.Maui.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Maui;

/// <summary>
/// Where an element of a MAUI application is found again.
/// </summary>
[TestClass]
public class NamesTests : BaseTestClass
{
	[TestMethod]
	public void ANamedElementIsAddressedByItsNameInTheWindow()
	{
		var button = new Button { AutomationId = "LoginButton" };

		_ = new Window(new ContentPage { Content = new VerticalStackLayout { button } });

		AreEqual(new UiNodeId("window:Window", "LoginButton"), UiAutomationNames.GetNodeId(button));
	}

	[TestMethod]
	public void ANamedWindowIsASurfaceOfThatName()
	{
		var window = new Window(new ContentPage()) { AutomationId = "Main" };

		AreEqual("window:Main", UiAutomationNames.GetSurfaceId(window));
		AreEqual(new UiNodeId("window:Main", "Main"), UiAutomationNames.GetNodeId(window));
	}

	[TestMethod]
	public void TwoUnnamedButtonsOfOnePageHaveAddressesOfTheirOwn()
	{
		var first = new Button();
		var second = new Button();

		_ = new Window(new ContentPage { Content = new VerticalStackLayout { first, second } });

		AreEqual("~/ContentPage[0]/VerticalStackLayout[0]/Button[0]", UiAutomationNames.GetNodeId(first).LocalId);
		AreEqual("~/ContentPage[0]/VerticalStackLayout[0]/Button[1]", UiAutomationNames.GetNodeId(second).LocalId);
	}

	[TestMethod]
	public void AnElementInNoWindowHasNoAddress()
		=> IsNull(UiAutomationNames.GetNodeId(new Button { AutomationId = "Orphan" }));

	[TestMethod]
	public void AScopeAnElementDeclaresNamesWhatIsInsideIt()
	{
		var label = new Label();
		var panel = new ContentView { Content = label };

		UiAutomationNames.SetScopeId(panel, "panel:orders");

		_ = new Window(new ContentPage { Content = panel });

		AreEqual(new UiNodeId("panel:orders", "~/Label[0]"), UiAutomationNames.GetNodeId(label));
	}

	[TestMethod]
	public void AShellEntryIsAddressedByTheRouteItWasDeclaredWith()
	{
		var orders = new ShellContent { Route = "orders", Content = new ContentPage() };
		var shell = new Shell();

		shell.Items.Add(orders);

		_ = new Window(shell);

		AreEqual(new UiNodeId("window:Window", "orders"), UiAutomationNames.GetNodeId(orders));

		// The item and the section the shell wrapped the entry in were named by nobody, so the names the shell
		// made up for them are not used as addresses.
		var item = shell.Items[0];

		IsTrue(UiAutomationNames.GetNodeId(item).LocalId.StartsWith('~'), UiAutomationNames.GetNodeId(item).LocalId);
	}

	[TestMethod]
	public void AnElementOnAPageFindsTheWindowItIsIn()
	{
		var label = new Label();
		var window = new Window(new ContentPage { Content = new Grid { label } });

		AreSame(window, UiAutomationNames.GetWindow(label));
	}
}
