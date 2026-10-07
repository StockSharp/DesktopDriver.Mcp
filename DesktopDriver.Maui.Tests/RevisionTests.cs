namespace StockSharp.DesktopDriver.Tests.Maui;

using System;
using System.Collections.ObjectModel;

using Ecng.UnitTesting;

using Microsoft.Maui.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Maui;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// Whether a wait sees a MAUI control change.
/// </summary>
[TestClass]
public class RevisionTests : BaseTestClass
{
	[TestMethod]
	public void TypingIntoAFieldMovesWhatItSays()
	{
		var entry = new Entry { AutomationId = "LoginEmailEntry" };

		_ = new Window(new ContentPage { Content = entry });

		var revisions = new UiRevisionTracker();

		using var binder = new MauiNodeBinder(new UiNodeRegistry(Guid.NewGuid()), revisions);

		var id = binder.Bind(entry).Id;
		var before = revisions.Read(id);

		entry.Text = "trader1@test.com";

		var after = revisions.Read(id);

		AreNotEqual(before.State, after.State);
		AreEqual(before.Layout, after.Layout);
	}

	[TestMethod]
	public void HidingAControlMovesWhatItShows()
	{
		var button = new Button { AutomationId = "LoginButton" };

		_ = new Window(new ContentPage { Content = button });

		var revisions = new UiRevisionTracker();

		using var binder = new MauiNodeBinder(new UiNodeRegistry(Guid.NewGuid()), revisions);

		var id = binder.Bind(button).Id;
		var before = revisions.Read(id);

		button.IsVisible = false;

		AreNotEqual(before.View, revisions.Read(id).View);
	}

	[TestMethod]
	public void AListFollowsTheRowsOfWhateverItIsGiven()
	{
		var first = new ObservableCollection<string> { "BTCUSDT" };
		var second = new ObservableCollection<string>();
		var list = new CollectionView { AutomationId = "Securities", ItemsSource = first };

		_ = new Window(new ContentPage { Content = list });

		var revisions = new UiRevisionTracker();

		using var binder = new MauiNodeBinder(new UiNodeRegistry(Guid.NewGuid()), revisions);

		var id = binder.Bind(list).Id;
		var before = revisions.Read(id);

		first.Add("ETHUSDT");

		var grown = revisions.Read(id);

		AreNotEqual(before.State, grown.State);

		// Given another collection, it moves with that one and no longer with the first.
		list.ItemsSource = second;

		var swapped = revisions.Read(id);

		first.Add("SOLUSDT");

		AreEqual(swapped.State, revisions.Read(id).State);

		second.Add("SOLUSDT");

		AreNotEqual(swapped.State, revisions.Read(id).State);
	}
}
