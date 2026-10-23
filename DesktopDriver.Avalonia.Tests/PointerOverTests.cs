namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Threading;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;

/// <summary>
/// What a control learns of the pointer coming over it and going away.
/// </summary>
/// <remarks>
/// A control that shows something only under the pointer - a chart's crosshair, a row's buttons - hides it again when it is
/// told the pointer went away. Input that only ever arrived never told it so: a chart clicked once kept its crosshair in
/// every picture taken afterwards, wherever the pointer had gone since.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class PointerOverTests : BaseTestClass
{
	private const string _synthetic = "synthetic";
	private const string _headless = "headless";

	private static Task RunAsync(Func<Task> body) => HeadlessRun.OnUiThread(body);

	private static AutomationFixture Fixture(string driver, Window window)
		=> driver == _synthetic ? AutomationFixture.Synthetic(window) : new AutomationFixture(window);

	private static Window NewWindow(string name, Control content, PixelPoint position)
	{
		var window = new Window
		{
			Name = name,
			Width = 400,
			Height = 300,
			Position = position,
			Content = content,
		};

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame();

		return window;
	}

	private static Border Spot(string name) => new() { Name = name, Width = 100, Height = 50, Background = Brushes.Gray };

	private static UiInputRequest Request(UiNodeId id, UiInputAction action)
		=> new(Guid.NewGuid(), UiTarget.FromId(id), UiControlPart.Instance, action, null, TimeSpan.FromSeconds(5));

	/// <summary>
	/// The pointer brought onto another control leaves the one it was over, which is told so once.
	/// </summary>
	[TestMethod]
	[Timeout(60000)]
	[DataRow(_synthetic)]
	[DataRow(_headless)]
	public Task ThePointerBroughtOntoAnotherControlLeavesTheFirst(string driver) => RunAsync(async () =>
	{
		var first = Spot("First");
		var second = Spot("Second");
		var left = 0;
		first.PointerExited += (_, _) => left++;

		var window = NewWindow("Pointed", new StackPanel { Orientation = Orientation.Horizontal, Children = { first, second } }, new(0, 0));

		using var fixture = Fixture(driver, window);

		await fixture.Input.ExecuteAsync(Request(fixture.Bind(first), UiHoverAction.Instance), CancellationToken.None);
		Dispatcher.UIThread.RunJobs();

		IsTrue(first.IsPointerOver, "the pointer brought onto a control is not over it");

		await fixture.Input.ExecuteAsync(Request(fixture.Bind(second), UiHoverAction.Instance), CancellationToken.None);
		Dispatcher.UIThread.RunJobs();

		IsFalse(first.IsPointerOver, "the control the pointer went away from still has it");
		IsTrue(second.IsPointerOver, "the pointer brought onto the second control is not over it");
		AreEqual(1, left, "the control the pointer went away from was not told so once");
	});

	/// <summary>
	/// A click is made with the pointer, which leaves what it was over on its way to what is clicked.
	/// </summary>
	[TestMethod]
	[Timeout(60000)]
	[DataRow(_synthetic)]
	[DataRow(_headless)]
	public Task AClickElsewhereTakesThePointerOffWhatItWasOver(string driver) => RunAsync(async () =>
	{
		var spot = Spot("Spot");
		var button = new Button { Name = "Press", Content = "Press" };

		var window = NewWindow("Clicked", new StackPanel { Orientation = Orientation.Horizontal, Children = { spot, button } }, new(0, 0));

		using var fixture = Fixture(driver, window);

		await fixture.Input.ExecuteAsync(Request(fixture.Bind(spot), UiHoverAction.Instance), CancellationToken.None);
		await fixture.Input.ExecuteAsync(
			Request(fixture.Bind(button), new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.None)),
			CancellationToken.None);
		Dispatcher.UIThread.RunJobs();

		IsFalse(spot.IsPointerOver, "the pointer that went to click elsewhere is still over the control it left");
		IsTrue(button.IsPointerOver, "the pointer that clicked a button is not over it");
	});

	/// <summary>
	/// The pointer taken off a window leaves every control in it it was over, the window as well.
	/// </summary>
	[TestMethod]
	[Timeout(60000)]
	[DataRow(_synthetic)]
	[DataRow(_headless)]
	public Task APointerTakenOffAWindowIsOverNothingInIt(string driver) => RunAsync(async () =>
	{
		var spot = Spot("Spot");
		var window = NewWindow("Left", new StackPanel { Children = { spot } }, new(0, 0));

		using var fixture = Fixture(driver, window);

		await fixture.Input.ExecuteAsync(Request(fixture.Bind(spot), UiHoverAction.Instance), CancellationToken.None);

		var receipt = await fixture.Input.ExecuteAsync(Request(fixture.Bind(window), UiLeaveAction.Instance), CancellationToken.None);
		Dispatcher.UIThread.RunJobs();

		IsTrue(receipt.AnyInputDispatched, "the pointer left a window without anything being sent to it");
		IsFalse(spot.IsPointerOver, "the control the pointer was over still has it");
		IsFalse(window.IsPointerOver, "the window the pointer was taken off still has it");
	});

	/// <summary>
	/// A window the pointer is not over is left with nothing sent to it.
	/// </summary>
	[TestMethod]
	[Timeout(60000)]
	[DataRow(_synthetic)]
	[DataRow(_headless)]
	public Task AWindowThePointerIsNotOverIsLeftWithNothingSent(string driver) => RunAsync(async () =>
	{
		var window = NewWindow("Untouched", new StackPanel { Children = { Spot("Spot") } }, new(0, 0));

		using var fixture = Fixture(driver, window);

		var receipt = await fixture.Input.ExecuteAsync(Request(fixture.Bind(window), UiLeaveAction.Instance), CancellationToken.None);

		IsFalse(receipt.AnyInputDispatched, "something was sent to a window the pointer was never over");
	});

	/// <summary>
	/// The pointer is in one window at a time: brought into another, it leaves the first - a popup's, a dialog's - as a hand
	/// carries it across.
	/// </summary>
	[TestMethod]
	[Timeout(60000)]
	[DataRow(_synthetic)]
	[DataRow(_headless)]
	public Task ThePointerBroughtIntoAnotherWindowLeavesTheFirst(string driver) => RunAsync(async () =>
	{
		var first = Spot("First");
		var second = Spot("Second");

		var owner = NewWindow("Owner", new StackPanel { Children = { first } }, new(0, 0));
		var other = NewWindow("Other", new StackPanel { Children = { second } }, new(500, 0));

		try
		{
			using var fixture = Fixture(driver, owner);
			fixture.Roots.Explicit = [owner, other];

			await fixture.Input.ExecuteAsync(Request(fixture.Bind(first), UiHoverAction.Instance), CancellationToken.None);
			await fixture.Input.ExecuteAsync(Request(fixture.Bind(second), UiHoverAction.Instance), CancellationToken.None);
			Dispatcher.UIThread.RunJobs();

			IsFalse(first.IsPointerOver, "the control in the window the pointer went away from still has it");
			IsFalse(owner.IsPointerOver, "the window the pointer went away from still has it");
			IsTrue(second.IsPointerOver, "the pointer brought into the other window is not over what it was brought onto");
		}
		finally
		{
			other.Close();
		}
	});
}
