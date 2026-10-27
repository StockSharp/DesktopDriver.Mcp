namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Threading;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Avalonia;
using StockSharp.DesktopDriver.Avalonia.Headless;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// What a control that has left its window reads as, and what becomes of input aimed at it.
/// </summary>
/// <remarks>
/// A list that rebuilds its rows takes the old controls out of the window and puts new ones in under the same
/// addresses a moment later. In between, the address still means the control that left: it is drawn nowhere,
/// and input aimed at it is refused the way input at a control not there yet is, so that a caller waiting for
/// the control waits for the one that replaces it.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class LeftTheWindowTests : BaseTestClass
{
	private static Task RunAsync(Func<Task> body) => HeadlessRun.OnUiThread(body);

	private static (Window Window, StackPanel Holder, Button Button) NewWindow()
	{
		var button = new Button { Name = "Leaving", Content = "Leaving", Width = 120, Height = 40 };
		var holder = new StackPanel { Name = "Holder", Children = { button } };
		var window = new Window { Name = "LeftWindow", Width = 400, Height = 300, Content = holder };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame();

		return (window, holder, button);
	}

	[TestMethod]
	[Timeout(60000)]
	public Task AControlTakenOutOfItsWindowIsNotVisible() => RunAsync(async () =>
	{
		var (window, holder, button) = NewWindow();

		using var fixture = new AutomationFixture(window);
		var id = fixture.Bind(button);

		holder.Children.Remove(button);
		Dispatcher.UIThread.RunJobs();

		var presentation = (await fixture.Snapshots.CaptureAsync(UiTarget.FromId(id), UiCaptureOptions.Default, CancellationToken.None)).Presentation;

		IsTrue(presentation.IsAttached is UiKnown<bool> { Value: false }, "The control is still said to be in a window.");
		IsTrue(presentation.IsEffectivelyVisible is UiKnown<bool> { Value: false }, "A control drawn in no window is said to be visible.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AControlOfAWindowThatClosedIsInNoWindow() => RunAsync(async () =>
	{
		// A window closed keeps its controls under it: the window opened again in its place holds new ones under the same
		// addresses, and the closed one's must not be taken for them.
		var (window, _, button) = NewWindow();

		using var fixture = new AutomationFixture(window);
		var id = fixture.Bind(button);

		window.Close();
		Dispatcher.UIThread.RunJobs();

		var presentation = (await fixture.Snapshots.CaptureAsync(UiTarget.FromId(id), UiCaptureOptions.Default, CancellationToken.None)).Presentation;

		IsTrue(presentation.IsAttached is UiKnown<bool> { Value: false }, "A control of a window that closed is said to be in a window.");
		IsTrue(presentation.IsEffectivelyVisible is UiKnown<bool> { Value: false }, "A control of a window that closed is said to be visible.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task TheWindowOfAControlIsToldOnAnyThread() => RunAsync(async () =>
	{
		// Whether an address still means the control it did is asked wherever a request is resolved, which is not the
		// thread of the interface.
		var (window, _, button) = NewWindow();

		AreSame(window, await Task.Run(() => AvaloniaTopLevels.Of(button)), "The window of a shown control is not told off the interface's thread.");

		window.Close();
		Dispatcher.UIThread.RunJobs();

		IsNull(await Task.Run(() => AvaloniaTopLevels.Of(button)), "A control of a window that closed is said to be in it off the interface's thread.");
	});

	[TestMethod]
	[Timeout(60000)]
	[DataRow(true)]
	[DataRow(false)]
	public Task InputAtAControlThatLeftItsWindowOnceAimedAtIsRefusedAsNotThere(bool synthetic) => RunAsync(async () =>
	{
		var (window, holder, button) = NewWindow();

		using var fixture = new AutomationFixture(window);
		var id = fixture.Bind(button);

		var request = new UiInputRequest(
			Guid.NewGuid(),
			UiTarget.FromId(id),
			UiControlPart.Instance,
			new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.None),
			null,
			TimeSpan.FromSeconds(5));

		// Aimed while the control is on screen, as the dispatcher aims before it hands the action to the backend.
		var aimed = new AvaloniaInputTargetResolver().Resolve(
			fixture.Nodes.Resolve(request.Target),
			request.Part,
			request.Action,
			new UiCaptureContext(fixture.Nodes.GetReference(id), fixture.Revisions.Read(id), UiCaptureOptions.Default));

		holder.Children.Remove(button);
		Dispatcher.UIThread.RunJobs();

		IUiInputDriver driver = synthetic
			? new AvaloniaSyntheticInputDriver(fixture.Nodes, fixture.Executor)
			: new HeadlessInputDriver(fixture.Nodes, fixture.Executor);

		var error = await ThrowsAsync<UiAutomationException>(() => driver.ExecuteAsync(request, aimed, CancellationToken.None));

		AreEqual(UiErrorCodes.NotCreated, error.Error.Code);
	});
}
