namespace StockSharp.DesktopDriver.Tests.Maui;

using Ecng.UnitTesting;

using Microsoft.Maui.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Maui;

/// <summary>
/// Whether a person could see a MAUI control and reach it, as far as the elements above it decide.
/// </summary>
[TestClass]
public class PresentationTests : BaseTestClass
{
	[TestMethod]
	public void DisablingALayoutDisablesWhatIsInIt()
	{
		var button = new Button();
		var form = new VerticalStackLayout { button };

		form.IsEnabled = false;

		IsFalse(MauiReach.IsEnabled(button));
	}

	[TestMethod]
	public void HidingALayoutHidesWhatIsInIt()
	{
		var button = new Button();
		var form = new VerticalStackLayout { button };

		form.IsVisible = false;

		IsFalse(MauiReach.IsVisible(button));
	}

	[TestMethod]
	public void ALayoutThatPassesThePointerOnPassesItOnForWhatIsInIt()
	{
		var button = new Button();
		var form = new VerticalStackLayout { button };

		form.InputTransparent = true;
		form.CascadeInputTransparent = true;

		IsFalse(MauiReach.TakesInput(button));

		// Kept to itself, it lets the pointer through to what is under it and still to what is inside it.
		form.CascadeInputTransparent = false;

		IsTrue(MauiReach.TakesInput(button));
	}
}
