namespace StockSharp.DesktopDriver.Tests.Maui;

using System;
using System.Linq;

using Ecng.UnitTesting;

using Microsoft.Maui.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Maui;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// What a reader is shown of a MAUI page.
/// </summary>
[TestClass]
public class BinderTests : BaseTestClass
{
	private static MauiNodeBinder Binder() => new(new UiNodeRegistry(Guid.NewGuid()), new UiRevisionTracker());

	[TestMethod]
	public void LayoutsArePassedThroughAndWhatTheyHoldIsShown()
	{
		var label = new Label { Text = "Email" };
		var entry = new Entry();
		var page = new ContentPage { Content = new Grid { new VerticalStackLayout { label, entry } } };

		_ = new Window(page);

		using var binder = Binder();

		CollectionAssert.AreEqual(new Element[] { label, entry }, binder.GetSemanticChildren(page, false).ToArray());
	}

	[TestMethod]
	public void ANamedLayoutIsShownLikeAControl()
	{
		var form = new VerticalStackLayout { AutomationId = "LoginForm" };
		var page = new ContentPage { Content = form };

		_ = new Window(page);

		using var binder = Binder();

		CollectionAssert.AreEqual(new Element[] { form }, binder.GetSemanticChildren(page, false).ToArray());
	}

	[TestMethod]
	public void EveryStandardControlIsReportedAsWhatItIs()
	{
		AreEqual("window", MauiNodeBinder.KindOf(new Window()));
		AreEqual("shell", MauiNodeBinder.KindOf(new Shell()));
		AreEqual("tabItem", MauiNodeBinder.KindOf(new ShellContent()));
		AreEqual("tabs", MauiNodeBinder.KindOf(new TabbedPage()));
		AreEqual("page", MauiNodeBinder.KindOf(new ContentPage()));
		AreEqual("button", MauiNodeBinder.KindOf(new Button()));
		AreEqual("toggle", MauiNodeBinder.KindOf(new CheckBox()));
		AreEqual("toggle", MauiNodeBinder.KindOf(new Switch()));
		AreEqual("textBox", MauiNodeBinder.KindOf(new Entry()));
		AreEqual("textBox", MauiNodeBinder.KindOf(new Editor()));
		AreEqual("comboBox", MauiNodeBinder.KindOf(new Picker()));
		AreEqual("list", MauiNodeBinder.KindOf(new CollectionView()));
		AreEqual("panel", MauiNodeBinder.KindOf(new ContentView()));
		AreEqual("control", MauiNodeBinder.KindOf(new Label()));
	}

	[TestMethod]
	public void AnElementInAWindowBindsToItsAddress()
	{
		var entry = new Entry { AutomationId = "LoginEmailEntry" };

		_ = new Window(new ContentPage { Content = entry });

		using var binder = Binder();

		var node = binder.Bind(entry);

		AreEqual("LoginEmailEntry", node.Id.LocalId);
		AreEqual("textBox", node.Kind);
	}
}
