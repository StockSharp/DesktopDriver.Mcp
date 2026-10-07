namespace StockSharp.DesktopDriver.Wpf;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Sends input into the running interface itself rather than through the windowing system.
/// </summary>
/// <remarks>
/// The events go in at the top of the interface's own input pipeline, so capture, focus and the handled
/// flag all behave as they do for a person. What is skipped is the layer below that: the operating
/// system's own delivery, and with it the question of whether this desktop would have accepted injected
/// input at all. Windows refuses injection outright on a locked session and from a process of lower
/// integrity than the window it aims at, and a run that is only ever started unattended would then never
/// test the product - it would test the desktop it happened to run on.
/// <para>
/// One click is the exception. A themed control works out what a click meant by asking where the pointer
/// is, and nothing here moves a pointer, so an ordinary left click on a whole control is delivered as the
/// control's own automation action - the one an accessibility tool would use - with focus moved first and
/// no pointer events raised. Everything else, and any click naming a part of a control, still goes in as
/// events. What a receipt reports is the same either way.
/// </para>
/// <para>
/// Where the target is is still decided the same way, by hit-testing the window at the point a pointer
/// would land: a control that is covered, disabled or off screen is refused before anything is sent.
/// </para>
/// <para>
/// Pointer position, button state and modifier state are read from the input devices rather than carried
/// on the event, so a handler that asks for them is told what the machine's own mouse and keyboard say.
/// A modifier is held by sending its own key down before the key it modifies and up after it.
/// </para>
/// </remarks>
public sealed class WpfSyntheticInputDriver(IUiNodeRegistry registry, IUiExecutor executor) : IUiInputDriver
{
	// Keys are named by their position on the keyboard, in the names the protocol uses for them, and the
	// list is closed: a name that is not here is refused, because guessing at one would press some other
	// key.
	private static readonly Dictionary<string, Key> _keys = new(StringComparer.OrdinalIgnoreCase)
	{
		["KeyA"] = Key.A, ["KeyB"] = Key.B, ["KeyC"] = Key.C,
		["KeyD"] = Key.D, ["KeyE"] = Key.E, ["KeyF"] = Key.F,
		["KeyG"] = Key.G, ["KeyH"] = Key.H, ["KeyI"] = Key.I,
		["KeyJ"] = Key.J, ["KeyK"] = Key.K, ["KeyL"] = Key.L,
		["KeyM"] = Key.M, ["KeyN"] = Key.N, ["KeyO"] = Key.O,
		["KeyP"] = Key.P, ["KeyQ"] = Key.Q, ["KeyR"] = Key.R,
		["KeyS"] = Key.S, ["KeyT"] = Key.T, ["KeyU"] = Key.U,
		["KeyV"] = Key.V, ["KeyW"] = Key.W, ["KeyX"] = Key.X,
		["KeyY"] = Key.Y, ["KeyZ"] = Key.Z,

		["Digit1"] = Key.D1, ["Digit2"] = Key.D2, ["Digit3"] = Key.D3,
		["Digit4"] = Key.D4, ["Digit5"] = Key.D5, ["Digit6"] = Key.D6,
		["Digit7"] = Key.D7, ["Digit8"] = Key.D8, ["Digit9"] = Key.D9,
		["Digit0"] = Key.D0,

		["Escape"] = Key.Escape, ["Backspace"] = Key.Back, ["Tab"] = Key.Tab,
		["Enter"] = Key.Enter, ["Space"] = Key.Space, ["Minus"] = Key.OemMinus,
		["Equal"] = Key.OemPlus, ["Comma"] = Key.OemComma, ["Period"] = Key.OemPeriod,
		["Slash"] = Key.OemQuestion, ["Semicolon"] = Key.OemSemicolon, ["Quote"] = Key.OemQuotes,
		["BracketLeft"] = Key.OemOpenBrackets, ["BracketRight"] = Key.OemCloseBrackets,
		["Backslash"] = Key.OemPipe, ["Backquote"] = Key.OemTilde,

		["F1"] = Key.F1, ["F2"] = Key.F2, ["F3"] = Key.F3,
		["F4"] = Key.F4, ["F5"] = Key.F5, ["F6"] = Key.F6,
		["F7"] = Key.F7, ["F8"] = Key.F8, ["F9"] = Key.F9,
		["F10"] = Key.F10, ["F11"] = Key.F11, ["F12"] = Key.F12,

		["Insert"] = Key.Insert, ["Delete"] = Key.Delete,
		["Home"] = Key.Home, ["End"] = Key.End,
		["PageUp"] = Key.PageUp, ["PageDown"] = Key.PageDown,
		["ArrowUp"] = Key.Up, ["ArrowDown"] = Key.Down,
		["ArrowLeft"] = Key.Left, ["ArrowRight"] = Key.Right,
		["NumpadEnter"] = Key.Enter,

		["ShiftLeft"] = Key.LeftShift, ["ControlLeft"] = Key.LeftCtrl,
		["AltLeft"] = Key.LeftAlt, ["MetaLeft"] = Key.LWin,
	};

	private readonly IUiNodeRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
	private readonly IUiExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

	// Milliseconds since the machine started, which is the scale real input is stamped on, and moving
	// forward by one for every event so that two sent in the same millisecond keep their order.
	private int _timestamp = Environment.TickCount;

	/// <inheritdoc />
	public string BackendId => "wpf.synthetic";

	/// <inheritdoc />
	public ImmutableArray<string> Capabilities { get; } =
		["input.click", "input.text", "input.key", "input.scroll"];

	/// <inheritdoc />
	public UiError Refusal => null;

	/// <inheritdoc />
	public Task<UiActionReceipt> ExecuteAsync(
		UiInputRequest request,
		UiResolvedInputTarget resolvedTarget,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(resolvedTarget);

		return _executor.InvokeAsync(() => Execute(request, resolvedTarget, cancellationToken), cancellationToken);
	}

	private UiActionReceipt Execute(
		UiInputRequest request,
		UiResolvedInputTarget resolvedTarget,
		CancellationToken cancellationToken)
	{
		var control = Resolve(resolvedTarget.Node?.Id)
			?? throw UiErrors.Fail(UiErrorCodes.InputUnavailable, "That node is not a control this backend can reach.");

		// The surface the point was measured against, which is not always the one the named control is on:
		// an open menu is a surface of its own, and an entry of it belongs to the ribbon that owns the
		// entry while being drawn on the menu. Hit-testing the window behind it would aim at whatever
		// happened to be at the same coordinates there.
		var root = Surface(resolvedTarget.SurfaceId)
			?? PresentationSource.FromVisual(control)?.RootVisual as UIElement
			?? throw UiErrors.Fail(UiErrorCodes.InputUnavailable, "That control is not in a window.");

		var point = new Point(resolvedTarget.PointInSurfaceDip.X, resolvedTarget.PointInSurfaceDip.Y);

		// Pointer events go to whatever is at the point, not to the node that was named. They are the same
		// thing for a button; they are not for a part of a compound control - a tab of a dock, a cell of a
		// table - where the node is the whole control and the point is the piece of it being aimed at.
		var under = root.InputHitTest(point) as UIElement ?? control;
		var dispatched = false;

		// Checked once more here, with the action about to go in: one cancelled while it queued must not
		// arrive afterwards.
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			switch (request.Action)
			{
				case UiClickAction click:
				{
					var button = click.Button switch
					{
						UiPointerButtons.Left => MouseButton.Left,
						UiPointerButtons.Middle => MouseButton.Middle,
						UiPointerButtons.Right => MouseButton.Right,
						_ => throw UiErrors.Invalid("That is not a pointer button."),
					};

					Move(under);

					var aimed = Aimed(request.Part, control, under, root, resolvedTarget);

					// Sent rather than carried out here. What a click leads to is the product's business and
					// can be a window that waits to be answered; a person's click is over the moment it is
					// made, and one that was not would hold this thread for as long as that window is up -
					// with nothing able to read the interface, close the window, or even be told the click
					// landed. It goes in at the priority the product's own work runs at, so it takes its turn
					// in the queue rather than behind everything that is reading meanwhile.
					control.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
					{
						// A themed control decides what a click meant by asking where the pointer is, and
						// nothing here moves a pointer. So an ordinary left click is offered to the control's
						// own automation action first - the same one an accessibility tool would use - and the
						// raw events are what happens when the control offers none.
						if (button == MouseButton.Left && click.Count <= 1 && Act(under, aimed))
							return;

						for (var index = 0; index < Math.Max(1, click.Count); index++)
						{
							Press(under, button);

							// The double click is raised off a click count that only the input manager can put
							// on the event, so the second press of a pair is promoted here, on the same
							// controls and in the same order the framework promotes it.
							if (index == 1)
							{
								DoubleClick(under, root, button);
								Gesture(under, root, button);
							}

							Release(under, button);
						}
					}));

					dispatched = true;

					break;
				}

				case UiTextAction text:
				{
					control.Focus();

					if (text.Mode == UiTextModes.Replace && !SelectAll(control))
					{
						throw UiErrors.Fail(
							UiErrorCodes.NotInteractable,
							"That control does not offer a way to replace what it holds.");
					}

					var composition = new TextComposition(InputManager.Current, control, text.Text ?? string.Empty);

					Raise(
						control,
						new TextCompositionEventArgs(InputManager.Current.PrimaryKeyboardDevice, composition),
						TextCompositionManager.PreviewTextInputEvent,
						TextCompositionManager.TextInputEvent);

					dispatched = true;

					break;
				}

				case UiKeyAction key:
				{
					control.Focus();

					// Named by the physical key rather than the character it produces: what a test means by
					// "press Enter" is a key on the keyboard, and which character that key types depends on
					// the layout the machine happens to have.
					if (!_keys.TryGetValue(key.Key ?? string.Empty, out var parsed))
						throw UiErrors.Invalid($"'{key.Key}' is not a key this backend knows.");

					Stroke(control, parsed, key.Modifiers);
					dispatched = true;

					break;
				}

				case UiScrollAction scroll:
				{
					// There is no sideways wheel in this framework's input.
					if (scroll.DeltaX != 0)
						throw UiErrors.Unsupported("Sideways scrolling is not something this backend sends.");

					Move(under);

					Raise(
						under,
						new MouseWheelEventArgs(
							InputManager.Current.PrimaryMouseDevice,
							Stamp(),
							(int)Math.Round(scroll.DeltaY * Mouse.MouseWheelDeltaForOneLine)),
						Mouse.PreviewMouseWheelEvent,
						Mouse.MouseWheelEvent);

					dispatched = true;

					break;
				}

				default:
					throw UiErrors.Unsupported($"{request.Action.Kind} is not something this backend sends.");
			}
		}
		catch (OperationCanceledException) when (LetGo(under, dispatched))
		{
			throw;
		}
		catch (Exception error)
		{
			LetGo(under, dispatched);

			// Nothing went in, so the caller can act on the refusal and try again knowing the interface
			// was not touched.
			if (!dispatched)
				throw;

			// Something did go in. Calling that a failure before dispatch would tell the caller it is safe
			// to repeat the action, and repeating half a click is how a test places two orders.
			return new UiActionReceipt(
				request.ActionId,
				UiActionStatuses.PartiallyDispatched,
				BackendId,
				UiField<UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
				UiField<UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
				true,
				error is UiAutomationException failure
					? failure.Error
					: UiError.Create(UiErrorCodes.OutcomeUnknown, error.Message));
		}

		return new UiActionReceipt(
			request.ActionId,
			UiActionStatuses.Dispatched,
			BackendId,
			UiField<UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
			UiField<UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
			dispatched,
			null);
	}

	// Walks up from what was aimed at until something says how it is operated. The walk is what makes a
	// click on the text inside a tab reach the tab: the text itself has no action of its own.
	private static UIElement Surface(string surfaceId)
	{
		if (string.IsNullOrEmpty(surfaceId))
			return null;

		foreach (PresentationSource source in PresentationSource.CurrentSources)
		{
			// A product can put a window up on a thread of its own - a splash screen is the usual one - and
			// reading anything at all off it from here is itself an error, so it is passed over.
			if (source.RootVisual is UIElement root
				&& root.CheckAccess()
				&& UiAutomationNames.GetSurfaceId(root as FrameworkElement) == surfaceId)
			{
				return root;
			}
		}

		return null;
	}

	// How far up the walk from the point may go, so that a click cannot escape into a container that
	// happens to be operable and do something nobody asked for. A whole control is bounded by itself, or
	// by the window when the point landed on it directly. A part of a compound control is addressed by the
	// rectangle the adapter worked out and by nothing else, so the walk is bounded by that rectangle: the
	// outermost thing that still covers no more than the part.
	private static UIElement Aimed(
		UiTargetPart part,
		UIElement control,
		UIElement under,
		UIElement root,
		UiResolvedInputTarget resolved)
	{
		if (part is UiControlPart)
			return ReferenceEquals(under, control) ? root : control;

		var bounds = resolved.BoundsInSurfaceDip;
		var ceiling = under;

		for (DependencyObject current = under; current is not null; current = VisualTreeHelper.GetParent(current))
		{
			if (current is not FrameworkElement element || !element.IsVisible)
				continue;

			var size = element.RenderSize;

			// A pixel of slack: the rectangle was measured on the same visual, and an ancestor that is the
			// same size as the part is still the part as far as an action goes.
			if (size.Width > bounds.Width + 1 || size.Height > bounds.Height + 1)
				break;

			ceiling = element;
		}

		return ceiling;
	}

	private static bool Act(UIElement element, UIElement ceiling)
	{
		for (DependencyObject current = element; current is not null; current = VisualTreeHelper.GetParent(current))
		{
			if (current is UIElement candidate && Operate(candidate))
				return true;

			if (ReferenceEquals(current, ceiling))
				break;
		}

		return false;
	}

	private static bool Operate(UIElement candidate)
	{
		var peer = UIElementAutomationPeer.CreatePeerForElement(candidate);

		if (peer is null)
			return false;

		// Doing comes first: an entry that does something when it is clicked does that, and being selected
		// or opened is what a click means only for the things that do nothing else.
		if (peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke)
		{
			Focus(candidate);
			invoke.Invoke();

			return true;
		}

		// A tab and a list item are not invoked but chosen, and what a click on one means is that it
		// becomes the chosen one.
		if (peer.GetPattern(PatternInterface.SelectionItem) is ISelectionItemProvider selection)
		{
			Focus(candidate);
			selection.Select();

			return true;
		}

		// Opening comes before toggling and after invoking: a menu that carries other entries is opened by
		// a click on it, and an entry that does something of its own does that instead.
		if (peer.GetPattern(PatternInterface.ExpandCollapse) is IExpandCollapseProvider expand)
		{
			Focus(candidate);
			expand.Expand();

			return true;
		}

		if (peer.GetPattern(PatternInterface.Toggle) is IToggleProvider toggle)
		{
			Focus(candidate);
			toggle.Toggle();

			return true;
		}

		return false;
	}

	// A click leaves the keyboard on what was clicked, and a test that clicks a field and then types
	// depends on it.
	private static void Focus(UIElement element)
	{
		if (element.Focusable)
			element.Focus();
	}

	// What is already there has to be selected before it can be typed over, and the keystroke a person
	// would use cannot be sent from inside the process. The control's own selection is asked for instead,
	// so one that refuses to be replaced still refuses.
	private static bool SelectAll(UIElement element)
	{
		if (element is TextBoxBase box)
		{
			if (box.IsReadOnly)
				return false;

			box.SelectAll();

			return true;
		}

		var peer = UIElementAutomationPeer.CreatePeerForElement(element);

		if (peer?.GetPattern(PatternInterface.Text) is ITextProvider provider)
		{
			provider.DocumentRange?.Select();

			return true;
		}

		return false;
	}

	private void Move(UIElement element)
		=> Raise(
			element,
			new MouseEventArgs(InputManager.Current.PrimaryMouseDevice, Stamp()),
			Mouse.PreviewMouseMoveEvent,
			Mouse.MouseMoveEvent);

	private void Press(UIElement element, MouseButton button)
	{
		Raise(
			element,
			new MouseButtonEventArgs(InputManager.Current.PrimaryMouseDevice, Stamp(), button),
			Mouse.PreviewMouseDownEvent,
			Mouse.MouseDownEvent);

		// And again as the event named after the button. On the real input path the framework promotes
		// the one into the other, and almost every control listens for the named one - a library's tab
		// or menu item hears nothing at all from the general pair alone.
		var named = DownEvents(button);

		if (named.Main is null)
			return;

		Raise(
			element,
			new MouseButtonEventArgs(InputManager.Current.PrimaryMouseDevice, Stamp(), button),
			named.Preview,
			named.Main);
	}

	private void Release(UIElement element, MouseButton button)
	{
		Raise(
			element,
			new MouseButtonEventArgs(InputManager.Current.PrimaryMouseDevice, Stamp(), button),
			Mouse.PreviewMouseUpEvent,
			Mouse.MouseUpEvent);

		var named = UpEvents(button);

		if (named.Main is null)
			return;

		Raise(
			element,
			new MouseButtonEventArgs(InputManager.Current.PrimaryMouseDevice, Stamp(), button),
			named.Preview,
			named.Main);
	}

	// The middle button has no event of its own in this framework, so it is left to the general pair.
	private static (RoutedEvent Preview, RoutedEvent Main) DownEvents(MouseButton button) => button switch
	{
		MouseButton.Left => (UIElement.PreviewMouseLeftButtonDownEvent, UIElement.MouseLeftButtonDownEvent),
		MouseButton.Right => (UIElement.PreviewMouseRightButtonDownEvent, UIElement.MouseRightButtonDownEvent),
		_ => default,
	};

	private static (RoutedEvent Preview, RoutedEvent Main) UpEvents(MouseButton button) => button switch
	{
		MouseButton.Left => (UIElement.PreviewMouseLeftButtonUpEvent, UIElement.MouseLeftButtonUpEvent),
		MouseButton.Right => (UIElement.PreviewMouseRightButtonUpEvent, UIElement.MouseRightButtonUpEvent),
		_ => default,
	};

	// Every control between the point and the window gets its own double click, and it names the element
	// that was hit as the source, because that is how the framework raises it: the row and the grid above
	// it both hear the pair, and each of them hears which part was aimed at.
	private void DoubleClick(UIElement element, DependencyObject root, MouseButton button)
	{
		for (var current = (DependencyObject)element; current is not null; current = VisualTreeHelper.GetParent(current))
		{
			if (current is Control control)
			{
				Raise(
					control,
					element,
					new MouseButtonEventArgs(InputManager.Current.PrimaryMouseDevice, Stamp(), button),
					Control.PreviewMouseDoubleClickEvent,
					Control.MouseDoubleClickEvent);
			}

			if (ReferenceEquals(current, root))
				break;
		}
	}

	// A gesture bound in markup - `<MouseBinding MouseAction="LeftDoubleClick" Command="..."/>` - is matched
	// by the framework against the click count on the event arguments, and only the input manager can put a
	// count there. Raising the events therefore never reaches the command, so the bindings are matched here
	// the same way, on the same chain, and the first one that will take it runs.
	private static void Gesture(UIElement element, DependencyObject root, MouseButton button)
	{
		var action = button switch
		{
			MouseButton.Left => MouseAction.LeftDoubleClick,
			MouseButton.Middle => MouseAction.MiddleDoubleClick,
			MouseButton.Right => MouseAction.RightDoubleClick,
			_ => MouseAction.None,
		};

		if (action == MouseAction.None)
			return;

		for (DependencyObject current = element; current is not null; current = VisualTreeHelper.GetParent(current))
		{
			if (current is UIElement candidate && Bound(candidate, action))
				return;

			if (ReferenceEquals(current, root))
				break;
		}
	}

	private static bool Bound(UIElement element, MouseAction action)
	{
		foreach (var binding in element.InputBindings)
		{
			if (binding is not InputBinding input || input.Gesture is not MouseGesture gesture)
				continue;

			// Only the plain gesture: one that asks for a modifier cannot be produced here at all, for the
			// same reason a modified keystroke cannot.
			if (gesture.MouseAction != action || gesture.Modifiers != ModifierKeys.None)
				continue;

			var command = input.Command;

			if (command is null || !command.CanExecute(input.CommandParameter))
				continue;

			command.Execute(input.CommandParameter);

			return true;
		}

		return false;
	}

	// Handlers in this framework read which modifiers are held from the keyboard device itself, not from
	// the event, and the device answers for the real keyboard. A modifier cannot be held here, so one that
	// was asked for is refused: sending the key without it would look like it worked and do something else.
	private void Stroke(UIElement element, Key key, UiKeyModifiers modifiers)
	{
		if (modifiers != UiKeyModifiers.None)
		{
			throw UiErrors.Unsupported(
				"Modifier keys cannot be held: WPF reads them from the keyboard device, which input put in at the top of the pipeline never touches.");
		}

		var source = Source(element);

		KeyDown(element, source, key);
		KeyUp(element, source, key);
	}

	private void KeyDown(UIElement element, PresentationSource source, Key key)
		=> Raise(
			element,
			new KeyEventArgs(InputManager.Current.PrimaryKeyboardDevice, source, Stamp(), key),
			Keyboard.PreviewKeyDownEvent,
			Keyboard.KeyDownEvent);

	private void KeyUp(UIElement element, PresentationSource source, Key key)
		=> Raise(
			element,
			new KeyEventArgs(InputManager.Current.PrimaryKeyboardDevice, source, Stamp(), key),
			Keyboard.PreviewKeyUpEvent,
			Keyboard.KeyUpEvent);

	// Runs as an exception filter so that whatever was pressed is let go before the exception leaves,
	// without swallowing it. A test that failed half way must not leave a control thinking it is held.
	private bool LetGo(UIElement element, bool dispatched)
	{
		if (dispatched)
		{
			try
			{
				Release(element, MouseButton.Left);
			}
			catch (Exception)
			{
				// Letting go is best effort; the original failure is the one worth reporting.
			}
		}

		return false;
	}

	private static void Raise(UIElement element, RoutedEventArgs args, RoutedEvent preview, RoutedEvent main)
		=> Raise(element, element, args, preview, main);

	// The tunnelling event first and the bubbling one only when nothing handled it, which is the order the
	// input manager sends them in: a control that swallows input in the preview swallows this too.
	private static void Raise(UIElement element, UIElement source, RoutedEventArgs args, RoutedEvent preview, RoutedEvent main)
	{
		// The event has to be named before the source is: this framework refuses a source on arguments
		// that do not yet say which event they belong to.
		args.RoutedEvent = preview;
		args.Source = source;
		element.RaiseEvent(args);

		if (args.Handled)
			return;

		args.RoutedEvent = main;
		args.Source = source;
		element.RaiseEvent(args);
	}

	private int Stamp() => unchecked(++_timestamp);

	private UIElement Resolve(UiNodeId id)
		=> id is null ? null : _registry.Resolve(UiTarget.FromId(id)).Instance as UIElement;

	// A key event carries the surface it arrived on, and there is none until the window is on screen.
	private static PresentationSource Source(UIElement element)
		=> PresentationSource.FromVisual(element)
			?? throw UiErrors.Fail(UiErrorCodes.InputUnavailable, "That control is not on a window that is on screen.");

	// The modifiers a request names, as the keys that hold them, in the order they are pressed: what a
	// handler reads is the state of the keyboard, so a modifier only counts as held once its key is down.
	private static ImmutableArray<Key> Modifiers(UiKeyModifiers modifiers)
	{
		var held = ImmutableArray.CreateBuilder<Key>();

		if (modifiers.HasFlag(UiKeyModifiers.Control))
			held.Add(Key.LeftCtrl);

		if (modifiers.HasFlag(UiKeyModifiers.Shift))
			held.Add(Key.LeftShift);

		if (modifiers.HasFlag(UiKeyModifiers.Alt))
			held.Add(Key.LeftAlt);

		if (modifiers.HasFlag(UiKeyModifiers.Meta))
			held.Add(Key.LWin);

		return held.ToImmutable();
	}
}
