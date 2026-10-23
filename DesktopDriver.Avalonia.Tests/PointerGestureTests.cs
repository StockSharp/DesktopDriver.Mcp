namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Input;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Threading;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Avalonia;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// A click with keys held, and a press carried from one place to another, reach a control as they would from a hand.
/// </summary>
/// <remarks>
/// A chart is traded on with both: Ctrl and a click place an order at the price under the pointer, and the line of an
/// order is taken hold of and carried to the price it is to move to. Each case runs with both kinds of input a product
/// is driven with: the headless platform's own, and the events a running product is given.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class PointerGestureTests : BaseTestClass
{
	// Takes the pointer the way a chart does - holds it from the press on - and notes what it was given.
	private sealed class Surface : Control
	{
		public List<(Point Position, KeyModifiers Keys, bool Left)> Presses { get; } = [];

		public List<Point> HeldMoves { get; } = [];

		public List<Point> Releases { get; } = [];

		// Painted, so that the pointer finds it: a control that draws nothing is not hit.
		public override void Render(DrawingContext context) => context.FillRectangle(Brushes.White, new Rect(Bounds.Size));

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			Presses.Add((e.GetPosition(this), e.KeyModifiers, e.GetCurrentPoint(this).Properties.IsLeftButtonPressed));
			e.Pointer.Capture(this);
			e.Handled = true;
		}

		protected override void OnPointerMoved(PointerEventArgs e)
		{
			if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
				HeldMoves.Add(e.GetPosition(this));
		}

		protected override void OnPointerReleased(PointerReleasedEventArgs e)
		{
			Releases.Add(e.GetPosition(this));
			e.Pointer.Capture(null);
		}
	}

	// Aims a price at the height inside the surface the price names, half way across.
	private sealed class SurfaceAdapter(AvaloniaNodeBinder binder) : AvaloniaControlAdapter(binder), IUiInputTargetAdapter
	{
		public override bool CanHandle(UiSubject subject) => subject?.Instance is Surface;

		public bool SupportsTargetPart(UiSubject subject, UiTargetPart part) => subject?.Instance is Surface && part is UiChartPricePart;

		public UiResolvedInputTarget ResolveInputTarget(UiSubject subject, UiTargetPart part, UiInputAction action, UiCaptureContext context)
		{
			var surface = (Surface)subject.Instance;
			var price = ((UiChartPricePart)part).Price;

			return AvaloniaInputTargetResolver.LocateAt(
				surface, new Point(surface.Bounds.Width / 2, (double)price), context?.Node, context?.Revisions, $"the price {price}");
		}
	}

	private static Task RunAsync(Func<Task> body) => HeadlessRun.OnUiThread(body);

	private static (Window Window, Surface Surface) NewWindow(Control cover)
	{
		var surface = new Surface { Name = "Surface", Width = 300, Height = 200 };
		var content = new Grid { Width = 300, Height = 200 };

		content.Children.Add(surface);

		if (cover is not null)
			content.Children.Add(cover);

		var window = new Window
		{
			Name = "GestureWindow",
			Width = 400,
			Height = 300,
			Content = content,
		};

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame()?.Dispose();

		return (window, surface);
	}

	private static AutomationFixture Fixture(Window window, bool synthetic)
	{
		var fixture = synthetic ? AutomationFixture.Synthetic(window) : new AutomationFixture(window);

		fixture.Adapters.Register("test.surface", typeof(Surface), new SurfaceAdapter(fixture.Binder), priority: 100);

		return fixture;
	}

	private static UiInputRequest Request(UiNodeId id, UiTargetPart part, UiInputAction action)
		=> new(Guid.NewGuid(), UiTarget.FromId(id), part, action, null, TimeSpan.FromSeconds(5));

	[TestMethod]
	[Timeout(60000)]
	[DataRow(false)]
	[DataRow(true)]
	public Task AClickReachesTheControlWithTheKeysHeldAndAtThePointAimedAt(bool synthetic) => RunAsync(async () =>
	{
		var (window, surface) = NewWindow(null);

		using var fixture = Fixture(window, synthetic);

		await fixture.Input.ExecuteAsync(
			Request(fixture.Bind(surface), new UiChartPricePart("Area0", 50m), new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.Control)),
			CancellationToken.None);
		Dispatcher.UIThread.RunJobs();

		var press = surface.Presses.Single();

		IsTrue(press.Keys.HasFlag(KeyModifiers.Control), $"The click arrived with {press.Keys} held, not Ctrl.");
		IsTrue(press.Left, "The click did not press the left button.");
		AreEqual(150d, press.Position.X, 0.5);
		AreEqual(50d, press.Position.Y, 0.5);
		AreEqual(1, surface.Releases.Count, "The button was not let go.");
	});

	[TestMethod]
	[Timeout(60000)]
	[DataRow(false)]
	[DataRow(true)]
	public Task APlainClickArrivesWithNoKeysHeld(bool synthetic) => RunAsync(async () =>
	{
		var (window, surface) = NewWindow(null);

		using var fixture = Fixture(window, synthetic);

		await fixture.Input.ExecuteAsync(
			Request(fixture.Bind(surface), new UiChartPricePart("Area0", 50m), new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.None)),
			CancellationToken.None);
		Dispatcher.UIThread.RunJobs();

		AreEqual(KeyModifiers.None, surface.Presses.Single().Keys);
	});

	[TestMethod]
	[Timeout(60000)]
	[DataRow(false)]
	[DataRow(true)]
	public Task APressIsCarriedThroughThePointsBetweenAndLetGoWhereAsked(bool synthetic) => RunAsync(async () =>
	{
		var (window, surface) = NewWindow(null);

		using var fixture = Fixture(window, synthetic);

		await fixture.Input.ExecuteAsync(
			Request(fixture.Bind(surface), new UiChartPricePart("Area0", 40m), new UiDragAction(new UiChartPricePart("Area0", 160m))),
			CancellationToken.None);
		Dispatcher.UIThread.RunJobs();

		AreEqual(40d, surface.Presses.Single().Position.Y, 0.5);
		AreEqual(160d, surface.Releases.Single().Y, 0.5);

		// Carried, not dropped there: a control that only starts a drag once the pointer has moved some way sees it move.
		IsTrue(surface.HeldMoves.Count >= 2, $"The pointer went from one end to the other in {surface.HeldMoves.Count} moves.");
		AreEqual(160d, surface.HeldMoves[^1].Y, 0.5);

		for (var index = 1; index < surface.HeldMoves.Count; index++)
			IsTrue(surface.HeldMoves[index].Y >= surface.HeldMoves[index - 1].Y, "The pointer went back on its way down.");
	});

	[TestMethod]
	[Timeout(60000)]
	[DataRow(false)]
	[DataRow(true)]
	public Task APointCoveredByAnotherControlIsRefusedAndNothingIsPressed(bool synthetic) => RunAsync(async () =>
	{
		// The lower half of the surface is under a panel, so the price drawn there cannot be pressed.
		var cover = new Border { Background = Brushes.Gray, Height = 100, VerticalAlignment = VerticalAlignment.Bottom };
		var (window, surface) = NewWindow(cover);

		using var fixture = Fixture(window, synthetic);

		var error = await ThrowsAsync<UiAutomationException>(() => fixture.Input.ExecuteAsync(
			Request(fixture.Bind(surface), new UiChartPricePart("Area0", 150m), new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.Control)),
			CancellationToken.None));

		AreEqual(UiErrorCodes.NotInteractable, error.Error.Code);
		AreEqual(0, surface.Presses.Count);
	});
}
