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
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Revisions move when the control does.
/// </summary>
/// <remarks>
/// Everything that asks "is this still what I saw" - a read guard, a wait, the consistency of a paged
/// read - compares revisions. A revision that never moves makes all of it decorative: a guard always
/// holds, and a wait for a change waits for ever while the change happens in front of it.
/// <para>
/// So the thing under test here is not the counter. It is that a real edit to a real control reaches it.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class RevisionMovementTests : BaseTestClass
{
	private const string _box = "watched.box";
	private const string _list = "watched.list";

	private static Task RunAsync(Action body) => HeadlessRun.OnUiThread(body);

	private static (Window Window, TextBox Box, ListBox List) NewWindow(ObservableCollection<string> items)
	{
		var box = new TextBox { Text = "before" };
		var list = new ListBox { ItemsSource = items };

		AutomationProperties.SetAutomationId(box, _box);
		AutomationProperties.SetAutomationId(list, _list);

		var window = new Window
		{
			Name = "WatchWindow",
			Width = 300,
			Height = 240,
			Content = new StackPanel { Children = { box, list } },
		};

		return (window, box, list);
	}

	private static void Settle(Window window)
	{
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame()?.Dispose();
		Dispatcher.UIThread.RunJobs();
	}

	private static UiRevisions Of(AutomationFixture fixture, string id)
		=> fixture.Revisions.Read(new UiNodeId("window:WatchWindow", id));

	private static void Bind(AutomationFixture fixture, params Control[] controls)
	{
		foreach (var control in controls)
			fixture.Binder.Bind(control);
	}

	[TestMethod]
	[Timeout(60000)]
	public Task TypingIntoABoxMovesItsState() => RunAsync(() =>
	{
		var items = new ObservableCollection<string> { "one" };
		var (window, box, list) = NewWindow(items);

		window.Show();
		Settle(window);

		using var fixture = new AutomationFixture(window);

		try
		{
			Bind(fixture, box, list);

			var before = Of(fixture, _box);

			box.Text = "after";
			Settle(window);

			var after = Of(fixture, _box);

			IsTrue(after.State.IsAfter(before.State), "The box was edited and its state did not move.");
		}
		finally
		{
			window.Close();
		}
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AddingARowMovesTheListsState() => RunAsync(() =>
	{
		var items = new ObservableCollection<string> { "one" };
		var (window, box, list) = NewWindow(items);

		window.Show();
		Settle(window);

		using var fixture = new AutomationFixture(window);

		try
		{
			Bind(fixture, box, list);

			var before = Of(fixture, _list);

			// Through the collection the control was given, which is how rows actually arrive in a product:
			// the control itself raises no property change for this at all.
			items.Add("two");
			Settle(window);

			IsTrue(Of(fixture, _list).State.IsAfter(before.State), "A row was added and the state did not move.");
		}
		finally
		{
			window.Close();
		}
	});

	[TestMethod]
	[Timeout(60000)]
	public Task SelectingARowMovesTheView() => RunAsync(() =>
	{
		var items = new ObservableCollection<string> { "one", "two" };
		var (window, box, list) = NewWindow(items);

		window.Show();
		Settle(window);

		using var fixture = new AutomationFixture(window);

		try
		{
			Bind(fixture, box, list);

			var before = Of(fixture, _list);

			list.SelectedIndex = 1;
			Settle(window);

			var after = Of(fixture, _list);

			IsTrue(after.View.IsAfter(before.View), "A row was selected and the view did not move.");

			// Choosing a row is not editing the data, and a caller that guarded on state should not be
			// refused for it.
			AreEqual(before.State.Version, after.State.Version);
		}
		finally
		{
			window.Close();
		}
	});

	[TestMethod]
	[Timeout(60000)]
	public Task ResizingMovesTheLayout() => RunAsync(() =>
	{
		var items = new ObservableCollection<string> { "one" };
		var (window, box, list) = NewWindow(items);

		window.Show();
		Settle(window);

		using var fixture = new AutomationFixture(window);

		try
		{
			Bind(fixture, box, list);

			var before = Of(fixture, _box);

			box.Width = 123;
			Settle(window);

			IsTrue(Of(fixture, _box).Layout.IsAfter(before.Layout), "The box was resized and the layout did not move.");
		}
		finally
		{
			window.Close();
		}
	});

	[TestMethod]
	[Timeout(60000)]
	public Task NothingHappeningMovesNothing() => RunAsync(() =>
	{
		var items = new ObservableCollection<string> { "one" };
		var (window, box, list) = NewWindow(items);

		window.Show();
		Settle(window);

		using var fixture = new AutomationFixture(window);

		try
		{
			Bind(fixture, box, list);

			var before = Of(fixture, _box);

			Settle(window);
			Settle(window);

			var after = Of(fixture, _box);

			// A counter that moved on its own would make every guard fail and every wait succeed.
			AreEqual(before.State.Version, after.State.Version);
			AreEqual(before.View.Version, after.View.Version);
		}
		finally
		{
			window.Close();
		}
	});
}
