namespace StockSharp.DesktopDriver.Avalonia.Headless;

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Input;

using StockSharp.DesktopDriver.Avalonia;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Sends input through Avalonia's headless platform.
/// </summary>
/// <remarks>
/// This is real input, not a shortcut: the events go in where the windowing system would put them and
/// travel the same path as a person's, so a disabled control refuses them and a covered one never sees
/// them. It is the backend for tests; a window on a real desktop is driven by the platform's own input.
/// </remarks>
public sealed class HeadlessInputDriver(IUiNodeRegistry registry, IUiExecutor executor) : IUiInputDriver
{
	private readonly IUiNodeRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
	private readonly IUiExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

	/// <inheritdoc />
	public string BackendId => "avalonia.headless";

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
		var control = Resolve(resolvedTarget.Node?.Id);
		var window = TopLevel.GetTopLevel(control) as Window
			?? throw UiErrors.Fail(UiErrorCodes.InputUnavailable, "That control is not in a headless window.");

		var point = new Point(resolvedTarget.PointInSurfaceDip.X, resolvedTarget.PointInSurfaceDip.Y);
		var dispatched = false;

		// Checked once more here, with the pointer about to move: an action cancelled while it queued
		// must not arrive afterwards.
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

					window.MouseMove(point);

					for (var index = 0; index < Math.Max(1, click.Count); index++)
					{
						window.MouseDown(point, button);
						dispatched = true;
						window.MouseUp(point, button);
					}

					break;
				}

				case UiTextAction text:
				{
					AvaloniaKeyboardTargets.ForText(request.Part, control, window, point)?.Focus();

					if (text.Mode == UiTextModes.Replace)
					{
						// Selected the way a person would, so that a control which refuses to be replaced
						// refuses here too instead of being overwritten behind its own back.
						window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
						window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.Control);
						dispatched = true;
					}

					window.KeyTextInput(text.Text ?? string.Empty);
					dispatched = true;

					break;
				}

				case UiKeyAction key:
				{
					AvaloniaKeyboardTargets.For(request.Part, control, window, point)?.Focus();

					// Named by the physical key rather than the character it produces: what a test means by
					// "press Enter" is a key on the keyboard, and which character that key types depends on
					// the layout the machine happens to have.
					if (!Enum.TryParse<PhysicalKey>(key.Key, ignoreCase: true, out var parsed))
						throw UiErrors.Invalid($"'{key.Key}' is not a key this backend knows.");

					var modifiers = ToRaw(key.Modifiers);

					window.KeyPressQwerty(parsed, modifiers);
					dispatched = true;
					window.KeyReleaseQwerty(parsed, modifiers);

					break;
				}

				case UiScrollAction scroll:
				{
					window.MouseMove(point);
					window.MouseWheel(point, new Vector(scroll.DeltaX, scroll.DeltaY));
					dispatched = true;

					break;
				}

				default:
					throw UiErrors.Unsupported($"{request.Action.Kind} is not something this backend sends.");
			}
		}
		catch (OperationCanceledException) when (Release(window, point, dispatched))
		{
			throw;
		}
		catch (Exception error)
		{
			Release(window, point, dispatched);

			// Nothing went out, so the caller can act on the refusal and try again knowing the interface
			// was not touched.
			if (!dispatched)
				throw;

			// Something did go out. Calling that a failure before dispatch would tell the caller it is
			// safe to repeat the action, and repeating half a click is how a test places two orders.
			return new UiActionReceipt(
				request.ActionId,
				UiActionStatuses.PartiallyDispatched,
				BackendId,
				UiField<Snapshots.UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
				UiField<Snapshots.UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
				true,
				error is UiAutomationException failure
					? failure.Error
					: UiError.Create(UiErrorCodes.OutcomeUnknown, error.Message));
		}

		return new UiActionReceipt(
			request.ActionId,
			UiActionStatuses.Dispatched,
			BackendId,
			UiField<Snapshots.UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
			UiField<Snapshots.UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
			dispatched,
			null);
	}

	// Runs as an exception filter so that whatever was pressed is let go before the exception leaves,
	// without swallowing it. A test that failed half way must not leave a button held down.
	private static bool Release(Window window, Point point, bool dispatched)
	{
		if (dispatched)
		{
			try
			{
				window.MouseUp(point, MouseButton.Left);
			}
			catch (Exception)
			{
				// Releasing is best effort; the original failure is the one worth reporting.
			}
		}

		return false;
	}

	private Control Resolve(UiNodeId id)
	{
		if (id is null)
			return null;

		return _registry.Resolve(UiTarget.FromId(id)).Instance as Control;
	}

	private static RawInputModifiers ToRaw(UiKeyModifiers modifiers)
	{
		var raw = RawInputModifiers.None;

		if (modifiers.HasFlag(UiKeyModifiers.Shift))
			raw |= RawInputModifiers.Shift;

		if (modifiers.HasFlag(UiKeyModifiers.Control))
			raw |= RawInputModifiers.Control;

		if (modifiers.HasFlag(UiKeyModifiers.Alt))
			raw |= RawInputModifiers.Alt;

		if (modifiers.HasFlag(UiKeyModifiers.Meta))
			raw |= RawInputModifiers.Meta;

		return raw;
	}
}
