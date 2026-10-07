namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

using Ecng.UnitTesting;

using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// A list says how much it is showing.
/// </summary>
/// <remarks>
/// A table answers this and so does a tree, but a plain list answered nothing at all - so a catalogue
/// built from one could not be told apart from the same catalogue before anything arrived in it, which
/// is exactly the question a case about it asks. The installer's products are such a list.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class ListCountTests : BaseTestClass
{
	private const string _list = "counted.list";

	private static Task RunAsync(Action body) => HeadlessRun.OnUiThread(body);

	private static void Settle(Window window)
	{
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame()?.Dispose();
		Dispatcher.UIThread.RunJobs();
	}

	private static (Window Window, ListBox List) NewWindow(ObservableCollection<string> items)
	{
		var list = new ListBox { ItemsSource = items };

		AutomationProperties.SetAutomationId(list, _list);

		var window = new Window
		{
			Name = "CountWindow",
			Width = 300,
			Height = 240,
			Content = list,
		};

		return (window, list);
	}

	private static UiState State(AutomationFixture fixture, Control control)
		=> fixture.Snapshots
			.CaptureAsync(UiTarget.FromId(fixture.Bind(control)), UiCaptureOptions.Default, CancellationToken.None)
			.GetAwaiter()
			.GetResult()
			.State;

	[TestMethod]
	[Timeout(60000)]
	public Task AListSaysHowManyItemsItIsShowing() => RunAsync(() =>
	{
		var items = new ObservableCollection<string> { "one", "two", "three" };
		var (window, list) = NewWindow(items);

		window.Show();
		Settle(window);

		using var fixture = new AutomationFixture(window);

		try
		{
			AreEqual(3L, State(fixture, list).Showing());

			items.Add("four");
			Settle(window);

			AreEqual(4L, State(fixture, list).Showing());
		}
		finally
		{
			window.Close();
		}
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AnEmptyListSaysNoneRatherThanNothing() => RunAsync(() =>
	{
		// Zero and "this control does not count anything" are different answers, and a case waiting for a
		// catalogue to fill has to be able to tell them apart.
		var (window, list) = NewWindow([]);

		window.Show();
		Settle(window);

		using var fixture = new AutomationFixture(window);

		try
		{
			AreEqual(0L, State(fixture, list).Showing());
		}
		finally
		{
			window.Close();
		}
	});

	[TestMethod]
	[Timeout(60000)]
	public Task WhatIsSelectedInAListIsReadable() => RunAsync(() =>
	{
		var items = new ObservableCollection<string> { "one", "two", "three" };
		var (window, list) = NewWindow(items);

		window.Show();
		Settle(window);

		using var fixture = new AutomationFixture(window);

		try
		{
			IsTrue(State(fixture, list) is ListState { SelectedCount: UiKnown<long> { Value: 0 } });

			list.SelectedIndex = 1;
			Settle(window);

			IsTrue(State(fixture, list) is ListState { SelectedCount: UiKnown<long> { Value: 1 } });
		}
		finally
		{
			window.Close();
		}
	});
}
