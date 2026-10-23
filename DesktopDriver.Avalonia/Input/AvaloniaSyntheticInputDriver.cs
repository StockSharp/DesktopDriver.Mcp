namespace StockSharp.DesktopDriver.Avalonia;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Automation.Peers;
using global::Avalonia.Automation.Provider;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.VisualTree;

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
/// </remarks>
public sealed class AvaloniaSyntheticInputDriver(IUiNodeRegistry registry, IUiExecutor executor) : IUiInputDriver
{
	private static readonly TimeSpan _drawPatience = TimeSpan.FromMilliseconds(500);

	private readonly IUiNodeRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
	private readonly IUiExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));
	private readonly Pointer _pointer = new(Pointer.GetNextFreeId(), PointerType.Mouse, true);

	private ulong _timestamp;

	// What the pointer is over, the window that is in and where in it. The framework keeps this itself only for the input
	// the windowing system gives it, and input sent here goes round that.
	private Control _over;
	private TopLevel _overRoot;
	private Point _overPoint;

	/// <inheritdoc />
	public string BackendId => "avalonia.synthetic";

	/// <inheritdoc />
	public ImmutableArray<string> Capabilities { get; } =
		["input.click", "input.text", "input.key", "input.scroll", "input.drag", "input.hover", "input.leave"];

	/// <inheritdoc />
	public UiError Refusal => null;

	/// <inheritdoc />
	/// <remarks>
	/// Avalonia answers what is under a point from what it has drawn, and draws on its own clock. A window that draws
	/// nothing - minimised, or on a desktop nobody looks at - is not waited for past a few frames' time.
	/// </remarks>
	public async Task DrawnAsync(UiTarget target, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(target);

		var drawn = await _executor.InvokeAsync(() => NextFrameDrawn(target), cancellationToken);

		await Task.WhenAny(drawn, Task.Delay(_drawPatience, cancellationToken));

		cancellationToken.ThrowIfCancellationRequested();
	}

	// The frame asked for first lays out and draws what is pending; the one asked for from it starts after that is drawn.
	private Task NextFrameDrawn(UiTarget target)
	{
		if (_registry.Resolve(target).Instance is not Visual visual || AvaloniaTopLevels.Of(visual) is not { } topLevel)
			return Task.CompletedTask;

		var drawn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		topLevel.RequestAnimationFrame(_ => topLevel.RequestAnimationFrame(_ => drawn.TrySetResult()));

		return drawn.Task;
	}

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

		var root = AvaloniaTopLevels.Of(control)
			?? throw UiErrors.Fail(UiErrorCodes.InputUnavailable, "That control is not in a window.");

		var point = new Point(resolvedTarget.PointInSurfaceDip.X, resolvedTarget.PointInSurfaceDip.Y);

		// Pointer events go to whatever is at the point, not to the node that was named. They are the same
		// thing for a button; they are not for a part of a compound control - a tab of a dock, a cell of a
		// table - where the node is the whole control and the point is the piece of it being aimed at.
		var under = AvaloniaTopLevels.HitTest(root, point) as Control ?? control;
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

					var keys = ToModifiers(click.Modifiers);

					Move(under, root, point, keys, RawInputModifiers.None);

					// A control is asked to do the thing a click does, through the same action an
					// accessibility tool would use, rather than being handed a pointer it may or may not
					// believe in. The pointer events below are what happens when a control names no action.
					// Only for the whole control: a part is identified by the point and by nothing else, and
					// an action found by walking up from it would be the container's, not the part's. And only
					// with no keys held: what a click with Ctrl means is the control's business, not its action's.
					if (button == MouseButton.Left
						&& click.Count <= 1
						&& click.Modifiers == UiKeyModifiers.None
						&& request.Part is UiControlPart
						&& Act(under, control, root))
					{
						dispatched = true;

						break;
					}

					for (var index = 0; index < Math.Max(1, click.Count); index++)
					{
						Press(under, root, point, button, index + 1, keys);
						dispatched = true;
						Release(Holder(under), root, point, button, keys);
					}

					break;
				}

				case UiDragAction:
				{
					var destination = resolvedTarget.DestinationInSurfaceDip
						?? throw UiErrors.Invalid("A drag came without the place to let it go.");

					var end = new Point(destination.X, destination.Y);

					Move(under, root, point, KeyModifiers.None, RawInputModifiers.None);
					Press(under, root, point, MouseButton.Left, 1, KeyModifiers.None);
					dispatched = true;

					// The rest of the gesture goes to whatever took hold of the pointer when it was pressed, as the
					// input system routes it, and passes through the points between the two ends.
					var holder = Holder(under);

					foreach (var step in AvaloniaDragPath.Between(point, end))
						Move(holder, root, step, KeyModifiers.None, RawInputModifiers.LeftMouseButton);

					Release(holder, root, end, MouseButton.Left, KeyModifiers.None);

					break;
				}

				case UiTextAction text:
				{
					var typedInto = AvaloniaKeyboardTargets.ForText(request.Part, control, root, point);

					typedInto.Focus();

					if (text.Mode == UiTextModes.Replace)
					{
						// Selected the way a person would, so that a control which refuses to be replaced
						// refuses here too instead of being overwritten behind its own back.
						Key(typedInto, global::Avalonia.Input.Key.A, KeyModifiers.Control);
						dispatched = true;
					}

					typedInto.RaiseEvent(new TextInputEventArgs
					{
						RoutedEvent = InputElement.TextInputEvent,
						Source = typedInto,
						Text = text.Text ?? string.Empty,
					});

					dispatched = true;

					break;
				}

				case UiKeyAction key:
				{
					var pressedOn = AvaloniaKeyboardTargets.For(request.Part, control, root, point);

					pressedOn.Focus();

					// Named by the physical key rather than the character it produces: what a test means by
					// "press Enter" is a key on the keyboard, and which character that key types depends on
					// the layout the machine happens to have.
					if (!Enum.TryParse<PhysicalKey>(key.Key, ignoreCase: true, out var parsed))
						throw UiErrors.Invalid($"'{key.Key}' is not a key this backend knows.");

					Key(pressedOn, parsed.ToQwertyKey(), ToModifiers(key.Modifiers));
					dispatched = true;

					break;
				}

				case UiHoverAction:
				{
					Move(under, root, point, KeyModifiers.None, RawInputModifiers.None);
					dispatched = true;

					break;
				}

				case UiLeaveAction:
				{
					// A window the pointer is not in has nothing of it to lose.
					if (_over is null || !ReferenceEquals(_overRoot, root))
						break;

					var left = Chain(_over);

					_over = null;
					_overRoot = null;

					Raise(InputElement.PointerExitedEvent, left, root, _overPoint, KeyModifiers.None);
					dispatched = true;

					break;
				}

				case UiScrollAction scroll:
				{
					Move(under, root, point, KeyModifiers.None, RawInputModifiers.None);

					under.RaiseEvent(new PointerWheelEventArgs(
						under,
						_pointer,
						root,
						point,
						Stamp(),
						new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
						KeyModifiers.None,
						new Vector(scroll.DeltaX, scroll.DeltaY)));

					dispatched = true;

					break;
				}

				default:
					throw UiErrors.Unsupported($"{request.Action.Kind} is not something this backend sends.");
			}
		}
		catch (OperationCanceledException) when (LetGo(under, root, point, dispatched))
		{
			throw;
		}
		catch (Exception error)
		{
			LetGo(under, root, point, dispatched);

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

	// Walks up from what was aimed at until something names an action. The walk is what makes a click on
	// the text inside a tab reach the tab: the text itself does nothing when clicked.
	private static bool Act(Control control, Control named, Visual root)
	{
		// How far up the walk may go. When the point landed inside the control that was addressed, that
		// control is as far as a click on it can mean anything; when it landed on the control itself, the
		// window is. Either way it stops somewhere, so a click cannot escape into a container that
		// happens to be operable and do something nobody asked for.
		var ceiling = ReferenceEquals(control, named) ? root : named;

		for (Visual current = control; current is not null; current = current.GetVisualParent())
		{
			// An entry of a list that drops down and an entry of a menu name actions that do part of what a click
			// does - choosing without closing the list, ticking without running the command - so they are clicked
			// with the pointer, as a person clicks them.
			if (current is ComboBoxItem or MenuItem)
				return false;

			if (current is Control candidate && Operate(candidate))
				return true;

			if (ReferenceEquals(current, ceiling))
				break;
		}

		return false;
	}

	private static bool Operate(Control candidate)
	{
		var peer = ControlAutomationPeer.CreatePeerForElement(candidate);

		if (peer is null)
			return false;

		// Selecting comes first: a tab and a list item can both be invoked and selected, and what a click
		// on one means is that it becomes the selected one.
		if (peer.GetProvider<ISelectionItemProvider>() is { } selection)
		{
			Focus(candidate);
			selection.Select();

			// Asked, and answered by nothing happening. A list item laid out by a repeater has no selecting
			// list above it, and the framework's own peer quietly does nothing in that case - so the item is
			// left for the pointer rather than reported as pressed.
			if (selection.IsSelected)
				return true;
		}

		if (peer.GetProvider<IInvokeProvider>() is { } invoke)
		{
			Focus(candidate);
			invoke.Invoke();

			return true;
		}

		if (peer.GetProvider<IToggleProvider>() is { } toggle)
		{
			Focus(candidate);
			toggle.Toggle();

			return true;
		}

		return false;
	}

	// A click leaves the keyboard on what was clicked, and a test that clicks a field and then types
	// depends on it.
	private static void Focus(Control control)
	{
		if (control.Focusable)
			control.Focus();
	}

	// The pointer belongs to whatever took hold of it on the press, and what follows the press goes there.
	private Control Holder(Control under) => _pointer.Captured as Control ?? under;

	private void Move(Control control, TopLevel root, Point point, KeyModifiers keys, RawInputModifiers buttons)
	{
		Over(control, root, point, keys);

		control.RaiseEvent(new PointerEventArgs(
			InputElement.PointerMovedEvent,
			control,
			_pointer,
			root,
			point,
			Stamp(),
			new PointerPointProperties(buttons | ToRaw(keys), PointerUpdateKind.Other),
			keys));
	}

	// The pointer comes over a control. What it leaves is told so, innermost first, and then what it comes over, as the
	// framework tells the movements of its own pointer; a window left for another is left entirely, unless it is gone.
	private void Over(Control control, TopLevel root, Point point, KeyModifiers keys)
	{
		if (ReferenceEquals(control, _over))
		{
			_overPoint = point;

			return;
		}

		var coming = Chain(control);
		var going = _overRoot?.PlatformImpl is null ? [] : Chain(_over);
		var shared = going.FirstOrDefault(element => coming.Contains(element));

		var left = going.TakeWhile(element => !ReferenceEquals(element, shared)).ToArray();
		var entered = coming.TakeWhile(element => !ReferenceEquals(element, shared)).ToArray();
		var leftRoot = _overRoot;
		var leftPoint = _overPoint;

		_over = control;
		_overRoot = root;
		_overPoint = point;

		Raise(InputElement.PointerExitedEvent, left, leftRoot, leftPoint, keys);
		Raise(InputElement.PointerEnteredEvent, entered, root, point, keys);
	}

	// The control and everything it sits in that takes input, innermost first.
	private static List<InputElement> Chain(Visual visual)
	{
		var chain = new List<InputElement>();

		for (var current = visual; current is not null; current = current.GetVisualParent())
		{
			if (current is InputElement element)
				chain.Add(element);
		}

		return chain;
	}

	// One event to each of the elements in turn, as the pointer's coming and going is a direct event to each of them.
	private void Raise(RoutedEvent<PointerEventArgs> routedEvent, IEnumerable<InputElement> elements, TopLevel root, Point point, KeyModifiers keys)
	{
		foreach (var element in elements)
		{
			element.RaiseEvent(new PointerEventArgs(
				routedEvent,
				element,
				_pointer,
				root,
				point,
				Stamp(),
				new PointerPointProperties(ToRaw(keys), PointerUpdateKind.Other),
				keys));
		}
	}

	private void Press(Control control, TopLevel root, Point point, MouseButton button, int count, KeyModifiers keys)
		=> control.RaiseEvent(new PointerPressedEventArgs(
			control,
			_pointer,
			root,
			point,
			Stamp(),
			new PointerPointProperties(Held(button) | ToRaw(keys), Down(button)),
			keys,
			count));

	private void Release(Control control, TopLevel root, Point point, MouseButton button, KeyModifiers keys)
		=> control.RaiseEvent(new PointerReleasedEventArgs(
			control,
			_pointer,
			root,
			point,
			Stamp(),
			new PointerPointProperties(ToRaw(keys), Up(button)),
			keys,
			button));

	private static void Key(Control control, Key key, KeyModifiers modifiers)
	{
		control.RaiseEvent(new KeyEventArgs
		{
			RoutedEvent = InputElement.KeyDownEvent,
			Source = control,
			Key = key,
			KeyModifiers = modifiers,
		});

		control.RaiseEvent(new KeyEventArgs
		{
			RoutedEvent = InputElement.KeyUpEvent,
			Source = control,
			Key = key,
			KeyModifiers = modifiers,
		});
	}

	// Runs as an exception filter so that whatever was pressed is let go before the exception leaves,
	// without swallowing it. A test that failed half way must not leave a control thinking it is held.
	private bool LetGo(Control control, TopLevel root, Point point, bool dispatched)
	{
		if (dispatched)
		{
			try
			{
				Release(Holder(control), root, point, MouseButton.Left, KeyModifiers.None);
			}
			catch (Exception)
			{
				// Letting go is best effort; the original failure is the one worth reporting.
			}
		}

		return false;
	}

	private ulong Stamp() => ++_timestamp;

	private Control Resolve(UiNodeId id)
		=> id is null ? null : _registry.Resolve(UiTarget.FromId(id)).Instance as Control;

	private static RawInputModifiers Held(MouseButton button) => button switch
	{
		MouseButton.Left => RawInputModifiers.LeftMouseButton,
		MouseButton.Middle => RawInputModifiers.MiddleMouseButton,
		MouseButton.Right => RawInputModifiers.RightMouseButton,
		_ => RawInputModifiers.None,
	};

	private static PointerUpdateKind Down(MouseButton button) => button switch
	{
		MouseButton.Left => PointerUpdateKind.LeftButtonPressed,
		MouseButton.Middle => PointerUpdateKind.MiddleButtonPressed,
		MouseButton.Right => PointerUpdateKind.RightButtonPressed,
		_ => PointerUpdateKind.Other,
	};

	private static PointerUpdateKind Up(MouseButton button) => button switch
	{
		MouseButton.Left => PointerUpdateKind.LeftButtonReleased,
		MouseButton.Middle => PointerUpdateKind.MiddleButtonReleased,
		MouseButton.Right => PointerUpdateKind.RightButtonReleased,
		_ => PointerUpdateKind.Other,
	};

	// The keys held, as the pointer's own state carries them alongside its buttons.
	private static RawInputModifiers ToRaw(KeyModifiers keys)
	{
		var result = RawInputModifiers.None;

		if (keys.HasFlag(KeyModifiers.Shift))
			result |= RawInputModifiers.Shift;

		if (keys.HasFlag(KeyModifiers.Control))
			result |= RawInputModifiers.Control;

		if (keys.HasFlag(KeyModifiers.Alt))
			result |= RawInputModifiers.Alt;

		if (keys.HasFlag(KeyModifiers.Meta))
			result |= RawInputModifiers.Meta;

		return result;
	}

	private static KeyModifiers ToModifiers(UiKeyModifiers modifiers)
	{
		var result = KeyModifiers.None;

		if (modifiers.HasFlag(UiKeyModifiers.Shift))
			result |= KeyModifiers.Shift;

		if (modifiers.HasFlag(UiKeyModifiers.Control))
			result |= KeyModifiers.Control;

		if (modifiers.HasFlag(UiKeyModifiers.Alt))
			result |= KeyModifiers.Alt;

		if (modifiers.HasFlag(UiKeyModifiers.Meta))
			result |= KeyModifiers.Meta;

		return result;
	}
}
