namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Threading;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;

/// <summary>
/// What a click means to the input a running product is driven with.
/// </summary>
/// <remarks>
/// That input works a click on a whole control through the control's own automation action, and the action a
/// control offers is not always what a click on it does: a list item offers to be selected, and a click on an
/// entry of a list that drops down also closes the list; a checkable menu entry offers to be toggled, and a
/// click on one also runs its command.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class SyntheticInputTests : BaseTestClass
{
	private sealed class CountingCommand : ICommand
	{
		public int Runs { get; private set; }

		public event EventHandler CanExecuteChanged { add { } remove { } }

		public bool CanExecute(object parameter) => true;

		public void Execute(object parameter) => Runs++;
	}

	private static Task RunAsync(Func<Task> body) => HeadlessRun.OnUiThread(body);

	private static Window NewWindow(Control content)
	{
		var window = new Window
		{
			Name = "SyntheticWindow",
			Width = 400,
			Height = 300,
			Content = content,
		};

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame();

		return window;
	}

	private static UiInputRequest Click(UiNodeId id)
		=> new(
			Guid.NewGuid(),
			UiTarget.FromId(id),
			UiControlPart.Instance,
			new UiClickAction(UiPointerButtons.Left, 1),
			null,
			TimeSpan.FromSeconds(5));

	/// <summary>
	/// An entry clicked in a list that drops down is chosen, and the list closes.
	/// </summary>
	[TestMethod]
	[Timeout(60000)]
	public Task AnEntryClickedInADroppedDownListIsChosenAndTheListCloses() => RunAsync(async () =>
	{
		var list = new ComboBox { Name = "Choices", Width = 200, ItemsSource = new[] { "Anonymous", "StockSharp", "Custom" }, SelectedIndex = 0 };

		var window = NewWindow(list);

		using var fixture = AutomationFixture.Synthetic(window);

		list.IsDropDownOpen = true;
		Dispatcher.UIThread.RunJobs();

		var custom = (ComboBoxItem)list.ContainerFromIndex(2);
		TopLevel.GetTopLevel(custom)?.CaptureRenderedFrame();

		await fixture.Input.ExecuteAsync(Click(fixture.Bind(custom)), CancellationToken.None);
		Dispatcher.UIThread.RunJobs();

		AreEqual(2, list.SelectedIndex, "the entry was not chosen");
		IsFalse(list.IsDropDownOpen, "the list was left open over the window");
	});

	/// <summary>
	/// A checkable menu entry clicked runs its command.
	/// </summary>
	[TestMethod]
	[Timeout(60000)]
	public Task ACheckableMenuEntryClickedRunsItsCommand() => RunAsync(async () =>
	{
		var command = new CountingCommand();
		var entry = new MenuItem
		{
			Name = "ServerMode",
			Header = "Server mode",
			ToggleType = MenuItemToggleType.CheckBox,
			Command = command,
		};

		var top = new MenuItem { Name = "Server", Header = "Server", ItemsSource = new[] { entry } };
		var window = NewWindow(new Menu { ItemsSource = new[] { top } });

		using var fixture = AutomationFixture.Synthetic(window);

		top.Open();
		Dispatcher.UIThread.RunJobs();
		TopLevel.GetTopLevel(entry)?.CaptureRenderedFrame();

		await fixture.Input.ExecuteAsync(Click(fixture.Bind(entry)), CancellationToken.None);
		Dispatcher.UIThread.RunJobs();

		AreEqual(1, command.Runs, "the entry was ticked without doing what it is for");
	});

	/// <summary>
	/// A plain menu entry clicked runs its command, as it did before checkable ones were clicked the same way.
	/// </summary>
	[TestMethod]
	[Timeout(60000)]
	public Task APlainMenuEntryClickedRunsItsCommand() => RunAsync(async () =>
	{
		var command = new CountingCommand();
		var entry = new MenuItem { Name = "Sources", Header = "Sources", Command = command };
		var top = new MenuItem { Name = "File", Header = "File", ItemsSource = new[] { entry } };
		var window = NewWindow(new Menu { ItemsSource = new[] { top } });

		using var fixture = AutomationFixture.Synthetic(window);

		top.Open();
		Dispatcher.UIThread.RunJobs();
		TopLevel.GetTopLevel(entry)?.CaptureRenderedFrame();

		await fixture.Input.ExecuteAsync(Click(fixture.Bind(entry)), CancellationToken.None);
		Dispatcher.UIThread.RunJobs();

		AreEqual(1, command.Runs);
	});

	/// <summary>
	/// The entries of an open context menu can be found and pressed.
	/// </summary>
	/// <remarks>
	/// A context menu keeps its popup to itself rather than in the window, and only popups found in the window were
	/// looked for: on a desktop, where the menu is a window of its own, its entries were nowhere. The headless
	/// platform draws the menu in the window's overlay, so this holds there either way; on a desktop it is what the
	/// menu being found through the control it was opened on is for.
	/// </remarks>
	[TestMethod]
	[Timeout(60000)]
	public Task AnOpenContextMenusEntriesCanBeFound() => RunAsync(() =>
	{
		var entry = new MenuItem { Name = "OpenData", Header = "Open data" };
		var target = new Border
		{
			Name = "Row",
			Width = 200,
			Height = 40,
			Background = global::Avalonia.Media.Brushes.Gray,
			ContextMenu = new ContextMenu { ItemsSource = new[] { entry } },
		};

		var window = NewWindow(target);

		using var fixture = AutomationFixture.Synthetic(window);

		target.ContextMenu.Open(target);
		Dispatcher.UIThread.RunJobs();

		IsNotNull(fixture.Tree.Locate(new UiNodeId("window:SyntheticWindow", "OpenData")));

		return Task.CompletedTask;
	});

	/// <summary>
	/// Text typed into a list that can be typed in goes into its box, as it does when a person clicks the list and
	/// types.
	/// </summary>
	/// <remarks>
	/// It was raised on the list itself, which edits nothing: the text went nowhere and the step reported success.
	/// </remarks>
	[TestMethod]
	[Timeout(60000)]
	public Task TextTypedIntoAListThatCanBeTypedInGoesIntoItsBox() => RunAsync(async () =>
	{
		var list = new ComboBox { Name = "Search", Width = 200, IsEditable = true, IsTextSearchEnabled = false, ItemsSource = new[] { "Best bid", "Best ask" } };

		var window = NewWindow(list);

		using var fixture = AutomationFixture.Synthetic(window);

		await fixture.Input.ExecuteAsync(
			new UiInputRequest(
				Guid.NewGuid(),
				UiTarget.FromId(fixture.Bind(list)),
				UiControlPart.Instance,
				new UiTextAction("best", UiTextModes.Append),
				null,
				TimeSpan.FromSeconds(5)),
			CancellationToken.None);
		Dispatcher.UIThread.RunJobs();

		AreEqual("best", list.Text);
	});

	/// <summary>
	/// A drop-down button clicked opens what it drops down, and what is in it can be found and pressed.
	/// </summary>
	/// <remarks>
	/// A flyout keeps its popup to itself as a context menu does, so on a desktop its content was nowhere. The
	/// headless platform draws it in the window's overlay; on a desktop it is found through the content it shows.
	/// </remarks>
	[TestMethod]
	[Timeout(60000)]
	public Task WhatADropDownButtonDropsDownCanBeFoundAndPressed() => RunAsync(async () =>
	{
		var option = new CheckBox { Name = "BestBid", Content = "Best bid" };
		var button = new DropDownButton
		{
			Name = "Fields",
			Content = "Fields",
			Flyout = new Flyout { Content = new StackPanel { Children = { option } } },
		};

		var window = NewWindow(button);

		using var fixture = AutomationFixture.Synthetic(window);

		await fixture.Input.ExecuteAsync(Click(fixture.Bind(button)), CancellationToken.None);
		Dispatcher.UIThread.RunJobs();
		TopLevel.GetTopLevel(option)?.CaptureRenderedFrame();

		IsTrue(button.Flyout.IsOpen, "the click did not drop the list down");

		await fixture.Input.ExecuteAsync(Click(fixture.Bind(option)), CancellationToken.None);
		Dispatcher.UIThread.RunJobs();

		IsTrue(option.IsChecked == true, "the option in the list was not pressed");
	});
}
