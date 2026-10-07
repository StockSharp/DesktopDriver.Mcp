namespace StockSharp.DesktopDriver.Tests.Maui;

using System;
using System.Collections.Generic;

using Ecng.UnitTesting;

using Microsoft.Maui.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Maui;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// What a MAUI control says about itself.
/// </summary>
[TestClass]
public class StateTests : BaseTestClass
{
	private static TState State<TState>(Element element)
		where TState : UiState
	{
		using var binder = new MauiNodeBinder(new UiNodeRegistry(Guid.NewGuid()), new UiRevisionTracker());

		var adapter = new MauiControlAdapter(binder);

		return (TState)adapter.CaptureState(new UiSubject(new UiNodeId("window:Window", "node"), element), null).State;
	}

	private static T Known<T>(UiField<T> field)
	{
		IsTrue(field.IsKnown, $"{field}");

		return ((UiKnown<T>)field).Value;
	}

	[TestMethod]
	public void AFieldSaysWhatIsTypedInItAndWhetherItCanBeChanged()
	{
		var state = State<BasicState>(new Entry { Text = "trader1@test.com", IsReadOnly = true });

		AreEqual("trader1@test.com", Known(state.Text));
		AreEqual(new UiStringValue("trader1@test.com"), Known(state.Value));
		IsTrue(Known(state.IsReadOnly));
	}

	[TestMethod]
	public void AToggleSaysWhetherItIsOn()
	{
		AreEqual(true, Known(State<BasicState>(new CheckBox { IsChecked = true }).IsChecked));
		AreEqual(false, Known(State<BasicState>(new Switch { IsToggled = false }).IsChecked));
		AreEqual(new UiBooleanValue(true), Known(State<BasicState>(new RadioButton { IsChecked = true }).Value));
	}

	[TestMethod]
	public void ABusyIndicatorSaysWhetherItIsTurning()
		=> AreEqual(new UiBooleanValue(true), Known(State<BasicState>(new ActivityIndicator { IsRunning = true }).Value));

	[TestMethod]
	public void APickerSaysWhatIsChosen()
	{
		var picker = new Picker { ItemsSource = new List<string> { "1m", "5m", "1h" } };

		picker.SelectedIndex = 1;

		AreEqual("5m", Known(State<BasicState>(picker).Text));
	}

	[TestMethod]
	public void ADatePickerHoldsADayWithNoClockInIt()
	{
		var state = State<BasicState>(new DatePicker { Date = new DateTime(2026, 9, 30, 15, 45, 0) });

		AreEqual(new UiTimestampValue(new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)), Known(state.Value));
	}

	[TestMethod]
	public void AListCountsWhatItWasGivenAndWhatIsChosen()
	{
		var list = new CollectionView
		{
			ItemsSource = new List<string> { "BTCUSDT", "ETHUSDT", "SOLUSDT" },
			SelectionMode = SelectionMode.Single,
		};

		list.SelectedItem = "ETHUSDT";

		var state = State<ListState>(list);

		AreEqual(3L, Known(state.ItemCount));
		AreEqual(1L, Known(state.SelectedIndex));
		AreEqual(1L, Known(state.SelectedCount));
		IsFalse(state.AllowsMultipleSelection);
	}

	[TestMethod]
	public void AListThatChoosesManySaysHowManyAreChosen()
	{
		var list = new CollectionView
		{
			ItemsSource = new List<string> { "BTCUSDT", "ETHUSDT", "SOLUSDT" },
			SelectionMode = SelectionMode.Multiple,
		};

		list.SelectedItems.Add("BTCUSDT");
		list.SelectedItems.Add("SOLUSDT");

		var state = State<ListState>(list);

		AreEqual(2L, Known(state.SelectedCount));
		IsTrue(state.AllowsMultipleSelection);
	}

	[TestMethod]
	public void AShellEntryIsSelectedWhileItIsTheOneShown()
	{
		var orders = new ShellContent { Route = "orders", Content = new ContentPage() };
		var trades = new ShellContent { Route = "trades", Content = new ContentPage() };
		var shell = new Shell();

		shell.Items.Add(orders);
		shell.Items.Add(trades);

		_ = new Window(shell);

		shell.CurrentItem = shell.Items[1];

		IsTrue(Known(State<BasicState>(shell.Items[1]).IsSelected));
		IsFalse(Known(State<BasicState>(shell.Items[0]).IsSelected));
	}
}
