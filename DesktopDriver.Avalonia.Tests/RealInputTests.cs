namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Templates;
using global::Avalonia.Headless;
using global::Avalonia.Layout;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// The vertical slice: a real window, real input, and the interface answering for itself afterwards.
/// </summary>
/// <remarks>
/// These are the tests the whole module exists to make possible. None of them calls a command, sets a
/// property or raises an event: each one puts input where a person would put it and then asks the
/// interface what happened.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class RealInputTests : BaseTestClass
{
	private static Task RunAsync(Func<Task> body) => HeadlessRun.OnUiThread(body);

	private static Window NewWindow(Control content)
	{
		var window = new Window
		{
			Name = "TestWindow",
			Width = 400,
			Height = 300,
			Content = content,
		};

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		// Where a pointer lands is answered by the composition, and the composition only exists once a
		// frame has been drawn.
		window.CaptureRenderedFrame();

		return window;
	}

	[TestMethod]
	[Timeout(60000)]
	public Task AClickOnARealButtonRunsWhatTheButtonDoes() => RunAsync(async () =>
	{
		var clicks = 0;
		var button = new Button { Name = "TheButton", Content = "Press me", Width = 120, Height = 40 };
		button.Click += (_, _) => clicks++;

		using var fixture = new AutomationFixture(NewWindow(button));
		var id = fixture.Bind(button);

		var receipt = await fixture.Input.ExecuteAsync(
			new UiInputRequest(
				Guid.NewGuid(),
				UiTarget.FromId(id),
				UiControlPart.Instance,
				new UiClickAction(UiPointerButtons.Left, 1),
				null,
				TimeSpan.FromSeconds(5)),
			CancellationToken.None);

		Dispatcher.UIThread.RunJobs();

		AreEqual(UiActionStatuses.Dispatched, receipt.Status);
		AreEqual("avalonia.headless", receipt.Backend);
		AreEqual(1, clicks, "The click went somewhere other than the button.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task TypingPutsTextIntoTheControlAndIntoWhateverItIsBoundTo() => RunAsync(async () =>
	{
		var box = new TextBox { Name = "TheBox", Width = 200, Height = 30 };

		using var fixture = new AutomationFixture(NewWindow(box));
		var id = fixture.Bind(box);

		await fixture.Input.ExecuteAsync(
			new UiInputRequest(
				Guid.NewGuid(),
				UiTarget.FromId(id),
				UiControlPart.Instance,
				new UiClickAction(UiPointerButtons.Left, 1),
				null,
				TimeSpan.FromSeconds(5)),
			CancellationToken.None);

		await fixture.Input.ExecuteAsync(
			new UiInputRequest(
				Guid.NewGuid(),
				UiTarget.FromId(id),
				UiControlPart.Instance,
				new UiTextAction("Привет ABC", UiTextModes.Append),
				null,
				TimeSpan.FromSeconds(5)),
			CancellationToken.None);

		Dispatcher.UIThread.RunJobs();

		AreEqual("Привет ABC", box.Text, "Cyrillic and Latin must both arrive as themselves.");

		// And the module reads back what is actually there, rather than what it sent.
		var snapshot = await fixture.Snapshots.CaptureAsync(
			UiTarget.FromId(id), UiCaptureOptions.Default, CancellationToken.None);

		var state = (BasicState)snapshot.State;

		AreEqual("Привет ABC", ((UiKnown<string>)state.Text).Value);
	});

	/// <summary>
	/// Text sent to a part of a control - a field inside an item of a tree, a property's editor - goes into that
	/// field. It went to the control as a whole, where nothing takes text, and the field stayed empty.
	/// </summary>
	[TestMethod]
	[Timeout(60000)]
	public Task TypingIntoAPartPutsTheTextIntoThatPart() => RunAsync(async () =>
	{
		var tree = new TreeView
		{
			Name = "TheTree",
			Width = 300,
			Height = 120,
			ItemsSource = new[] { "First" },
			ItemTemplate = new FuncDataTemplate<string>((name, _) => new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Children =
				{
					new TextBlock { Text = name, Width = 60 },
					new TextBox { Name = "Box", Width = 150 },
				},
			}),
		};

		var window = NewWindow(tree);

		// Typing goes into the window a person is looking at.
		window.Activate();

		using var fixture = new AutomationFixture(window);
		var id = fixture.Bind(tree);

		// A tree builds its rows after the window's first frame and a field gets its template when it is first
		// drawn, while where a pointer lands is answered by the frame drawn last - so two more frames.
		for (var frame = 0; frame < 2; frame++)
		{
			Dispatcher.UIThread.RunJobs();
			window.UpdateLayout();
			window.CaptureRenderedFrame()?.Dispose();
		}

		var receipt = await fixture.Input.ExecuteAsync(
			new UiInputRequest(
				Guid.NewGuid(),
				UiTarget.FromId(id),
				new UiTreeItemControlPart("First", "Box"),
				new UiTextAction("BTCUSDT", UiTextModes.Replace),
				null,
				TimeSpan.FromSeconds(5)),
			CancellationToken.None);

		Dispatcher.UIThread.RunJobs();

		var box = tree.GetVisualDescendants().OfType<TextBox>().Single();

		AreEqual(UiActionStatuses.Dispatched, receipt.Status);
		AreEqual("BTCUSDT", box.Text);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task ADisabledButtonIsRefusedAndItsHandlerNeverRuns() => RunAsync(async () =>
	{
		// The whole reason input has to be real: a disabled button reached through its own Click event
		// would run, and the test would pass while the user could not do the same thing.
		var clicks = 0;
		var button = new Button { Name = "TheButton", Content = "No", IsEnabled = false, Width = 120, Height = 40 };
		button.Click += (_, _) => clicks++;

		using var fixture = new AutomationFixture(NewWindow(button));
		var id = fixture.Bind(button);

		var error = await ThrowsAsync<UiAutomationException>(() => fixture.Input.ExecuteAsync(
			new UiInputRequest(
				Guid.NewGuid(),
				UiTarget.FromId(id),
				UiControlPart.Instance,
				new UiClickAction(UiPointerButtons.Left, 1),
				null,
				TimeSpan.FromSeconds(5)),
			CancellationToken.None));

		AreEqual(UiErrorCodes.NotInteractable, error.Error.Code);
		AreEqual(0, clicks);
	});

	/// <summary>
	/// A control further down a scrolled form is brought onto the screen, and can be clicked afterwards.
	/// </summary>
	/// <remarks>
	/// Only the parts of tables, trees and docks could be shown. A plain control below the fold of a form was
	/// refused as covered, by whatever lay over the place it would have been, and a picture of it showed that.
	/// </remarks>
	[TestMethod]
	[Timeout(60000)]
	public Task AControlBelowTheFoldIsBroughtOntoTheScreen() => RunAsync(async () =>
	{
		var clicks = 0;
		var button = new Button { Name = "FarDown", Content = "Far down", Width = 120, Height = 40 };
		button.Click += (_, _) => clicks++;

		var scroller = new ScrollViewer
		{
			Height = 200,
			VerticalAlignment = VerticalAlignment.Top,
			Content = new StackPanel { Children = { new Border { Height = 600 }, button } },
		};

		var window = NewWindow(scroller);

		using var fixture = new AutomationFixture(window);
		var id = fixture.Bind(button);

		await fixture.Input.ExecuteAsync(
			new UiInputRequest(
				Guid.NewGuid(),
				UiTarget.FromId(id),
				UiControlPart.Instance,
				UiEnsureVisibleAction.Instance,
				null,
				TimeSpan.FromSeconds(5)),
			CancellationToken.None);

		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame();

		AreEqual(0, clicks, "Showing the control pressed it.");
		IsTrue(scroller.Offset.Y > 0, "The form was not scrolled.");

		var receipt = await fixture.Input.ExecuteAsync(
			new UiInputRequest(
				Guid.NewGuid(),
				UiTarget.FromId(id),
				UiControlPart.Instance,
				new UiClickAction(UiPointerButtons.Left, 1),
				null,
				TimeSpan.FromSeconds(5)),
			CancellationToken.None);

		Dispatcher.UIThread.RunJobs();

		AreEqual(UiActionStatuses.Dispatched, receipt.Status);
		AreEqual(1, clicks, "The control was still out of reach.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AControlCoveredByAnotherOneIsRefused() => RunAsync(async () =>
	{
		var clicks = 0;
		var button = new Button { Name = "Covered", Content = "Hidden", Width = 120, Height = 40 };
		button.Click += (_, _) => clicks++;

		var cover = new Border
		{
			Background = global::Avalonia.Media.Brushes.Red,
			Width = 400,
			Height = 300,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
		};

		var grid = new Grid();
		grid.Children.Add(button);
		grid.Children.Add(cover);

		using var fixture = new AutomationFixture(NewWindow(grid));
		var id = fixture.Bind(button);

		var error = await ThrowsAsync<UiAutomationException>(() => fixture.Input.ExecuteAsync(
			new UiInputRequest(
				Guid.NewGuid(),
				UiTarget.FromId(id),
				UiControlPart.Instance,
				new UiClickAction(UiPointerButtons.Left, 1),
				null,
				TimeSpan.FromSeconds(5)),
			CancellationToken.None));

		AreEqual(UiErrorCodes.NotInteractable, error.Error.Code);
		AreEqual(0, clicks, "The click landed on something the user could not have reached.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AnActionCancelledBeforeItIsSentDoesNotArriveLater() => RunAsync(async () =>
	{
		var clicks = 0;
		var button = new Button { Name = "TheButton", Content = "Press me", Width = 120, Height = 40 };
		button.Click += (_, _) => clicks++;

		using var fixture = new AutomationFixture(NewWindow(button));
		var id = fixture.Bind(button);
		using var cancellation = new CancellationTokenSource();

		cancellation.Cancel();

		await ThrowsAsync<OperationCanceledException>(() => fixture.Input.ExecuteAsync(
			new UiInputRequest(
				Guid.NewGuid(),
				UiTarget.FromId(id),
				UiControlPart.Instance,
				new UiClickAction(UiPointerButtons.Left, 1),
				null,
				TimeSpan.FromSeconds(5)),
			cancellation.Token));

		Dispatcher.UIThread.RunJobs();

		AreEqual(0, clicks);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task TheSameActionIdentifierNeverClicksTwice() => RunAsync(async () =>
	{
		// What makes a dropped connection safe: the caller retries the question, not the click.
		var clicks = 0;
		var button = new Button { Name = "TheButton", Content = "Press me", Width = 120, Height = 40 };
		button.Click += (_, _) => clicks++;

		using var fixture = new AutomationFixture(NewWindow(button));
		var id = fixture.Bind(button);
		var actionId = Guid.NewGuid();

		UiInputRequest Request() => new(
			actionId,
			UiTarget.FromId(id),
			UiControlPart.Instance,
			new UiClickAction(UiPointerButtons.Left, 1),
			null,
			TimeSpan.FromSeconds(5));

		await fixture.Input.ExecuteAsync(Request(), CancellationToken.None);
		var again = await fixture.Input.ExecuteAsync(Request(), CancellationToken.None);

		Dispatcher.UIThread.RunJobs();

		AreEqual(1, clicks);
		AreEqual(UiActionStatuses.Dispatched, again.Status);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AToggleReportsItsThreeStatesRatherThanTwo() => RunAsync(async () =>
	{
		var toggle = new CheckBox { Name = "TheToggle", IsThreeState = true, IsChecked = null, Content = "Maybe" };

		using var fixture = new AutomationFixture(NewWindow(toggle));
		var id = fixture.Bind(toggle);

		var snapshot = await fixture.Snapshots.CaptureAsync(
			UiTarget.FromId(id), UiCaptureOptions.Default, CancellationToken.None);

		var state = (BasicState)snapshot.State;

		IsTrue(state.IsChecked.IsKnown, "An indeterminate toggle is a known state, not an unreadable one.");
		IsNull(((UiKnown<bool?>)state.IsChecked).Value);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AControlCanBeAddressedBeforeAnythingHasReadIt() => RunAsync(async () =>
	{
		// A test names the control it is about and nothing else. Making it walk the whole interface first,
		// purely so the module notices the control exists, would be a step with nothing to do with what
		// the test is testing - and one that is easy to forget and hard to diagnose when it is.
		var clicks = 0;
		var button = new Button { Name = "DeepButton", Content = "Press me", Width = 120, Height = 40 };
		button.Click += (_, _) => clicks++;

		var panel = new StackPanel { Name = "Outer" };
		panel.Children.Add(new StackPanel { Name = "Inner", Children = { button } });

		using var fixture = new AutomationFixture(NewWindow(panel));

		// Deliberately not bound, walked or captured first.
		var target = UiTarget.FromId(new UiNodeId("window:TestWindow", "DeepButton"));

		var snapshot = await fixture.Snapshots.CaptureAsync(target, UiCaptureOptions.Default, CancellationToken.None);

		AreEqual("button", snapshot.Node.Kind);

		await fixture.Input.ExecuteAsync(
			new UiInputRequest(
				Guid.NewGuid(),
				target,
				UiControlPart.Instance,
				new UiClickAction(UiPointerButtons.Left, 1),
				null,
				TimeSpan.FromSeconds(5)),
			CancellationToken.None);

		Dispatcher.UIThread.RunJobs();

		AreEqual(1, clicks);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AnAddressNoControlHasIsStillRefused() => RunAsync(async () =>
	{
		// Searching for a node must not turn a wrong address into some other node that happens to be
		// nearby.
		using var fixture = new AutomationFixture(NewWindow(new Button { Name = "TheButton", Width = 80, Height = 30 }));

		var error = await ThrowsAsync<UiAutomationException>(() => fixture.Snapshots.CaptureAsync(
			UiTarget.FromId(new UiNodeId("window:TestWindow", "NoSuchControl")),
			UiCaptureOptions.Default,
			CancellationToken.None));

		AreEqual(UiErrorCodes.NotFound, error.Error.Code);
	});
}
