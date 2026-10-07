namespace StockSharp.DesktopDriver.Maui;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Maui;
using Microsoft.Maui.Controls;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

using MauiPoint = Microsoft.Maui.Graphics.Point;
using WinElement = Microsoft.UI.Xaml.FrameworkElement;
using WinFlyoutShowOptions = Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions;
using WinPoint = Windows.Foundation.Point;
using WinUIElement = Microsoft.UI.Xaml.UIElement;

/// <summary>
/// Carries a click, a typed text, a key and a turn of the wheel into a MAUI application.
/// </summary>
/// <remarks>
/// Nothing here moves a pointer or presses a key of the machine. A click is carried out through what the
/// control under the point offers for one - the action its platform control gives an accessibility tool, or
/// the tap its MAUI recognizers wait for - which leads where a person's click leads. Text goes into the field
/// the way the field's own handler puts typed text there.
/// </remarks>
/// <param name="registry">Where the named node is looked up.</param>
/// <param name="executor">Runs everything on the interface's thread.</param>
public sealed class MauiInputDriver(IUiNodeRegistry registry, MauiUiExecutor executor) : IUiInputDriver
{
	// How far one notch of the wheel moves a scrolling area, which is what the platform moves one by.
	private const double _notch = 48;

	private readonly IUiNodeRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
	private readonly MauiUiExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

	/// <inheritdoc />
	public string BackendId => "maui.windows";

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
		var element = Resolve(resolvedTarget.Node?.Id)
			?? throw UiErrors.Fail(UiErrorCodes.InputUnavailable, "That node is not an element this backend can reach.");

		var window = Surface(resolvedTarget.SurfaceId)
			?? UiAutomationNames.GetWindow(element)
			?? throw UiErrors.Fail(UiErrorCodes.InputUnavailable, "That element is not in a window.");

		var point = new MauiPoint(resolvedTarget.PointInSurfaceDip.X, resolvedTarget.PointInSurfaceDip.Y);
		var hits = MauiPlatform.At(window, point);

		// What is on top at the point, which is not always the node that was named: for a part of a compound
		// control - a row of a list, an entry of a flyout - the node is the whole control and the point is the
		// piece of it being aimed at.
		var under = hits.Count > 0 ? hits[0] : MauiPlatform.ViewOf(element);

		if (under is null)
			throw UiErrors.Fail(UiErrorCodes.InputUnavailable, "Nothing is drawn at that point.");

		// Checked once more here, with the action about to go in: one cancelled while it queued must not
		// arrive afterwards.
		cancellationToken.ThrowIfCancellationRequested();

		switch (request.Action)
		{
			case UiClickAction click:
			{
				if (click.Button == UiPointerButtons.Middle)
					throw UiErrors.Unsupported("Nothing in a MAUI application answers the middle button.");

				var ceiling = Aimed(request.Part, MauiPlatform.ViewOf(element), under, MauiPlatform.RootOf(window), resolvedTarget);

				// Sent rather than carried out here. What a click leads to is the product's business and can be a
				// question that waits to be answered; a person's click is over the moment it is made, and one that
				// was not would hold this thread for as long as the question is up.
				window.Dispatcher.Dispatch(() => Click(window, under, ceiling, point, click));

				break;
			}

			case UiTextAction text:
			{
				var field = TextField(element, window, hits);

				if (field.IsReadOnly)
					throw UiErrors.Fail(UiErrorCodes.NotInteractable, "That field cannot be typed into.");

				field.Focus();
				field.Text = text.Mode == UiTextModes.Replace ? text.Text ?? string.Empty : (field.Text ?? string.Empty) + text.Text;

				break;
			}

			case UiKeyAction key:
			{
				var field = TextField(element, window, hits);

				if (key.Modifiers != UiKeyModifiers.None)
					throw UiErrors.Unsupported("Keys are delivered here only on their own, without a modifier held.");

				MauiKeys.Press(field, key.Key);

				break;
			}

			case UiScrollAction scroll:
			{
				var scroller = MauiPlatform.ScrollerOf(under)
					?? throw UiErrors.Fail(UiErrorCodes.NotInteractable, "Nothing under that point scrolls.");

				// A notch away from the reader moves the content back towards its start, as the wheel does.
				scroller.ChangeView(
					scroller.HorizontalOffset - scroll.DeltaX * _notch,
					scroller.VerticalOffset - scroll.DeltaY * _notch,
					null,
					true);

				break;
			}

			default:
				throw UiErrors.Unsupported($"{request.Action.Kind} is not something this backend sends.");
		}

		return new UiActionReceipt(
			request.ActionId,
			UiActionStatuses.Dispatched,
			BackendId,
			UiField<UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
			UiField<UiRevisions>.Unavailable(UiUnavailableReasons.Unknown, null),
			true,
			null);
	}

	private Element Resolve(UiNodeId id)
		=> id is not null && _registry.TryResolve(id, out var subject) ? subject.Instance as Element : null;

	private static Window Surface(string surfaceId)
		=> string.IsNullOrEmpty(surfaceId)
			? null
			: Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => UiAutomationNames.GetSurfaceId(window) == surfaceId);

	// How far up the walk from the point may go, so that a click cannot escape into a container that happens
	// to be operable and do something nobody asked for. A whole element is bounded by itself, or by the window
	// when the point landed on it directly. A part of a compound control is addressed by the rectangle the
	// adapter worked out and by nothing else, so the walk is bounded by that rectangle: the outermost thing
	// that still covers no more than the part.
	private static WinUIElement Aimed(
		UiTargetPart part,
		WinElement view,
		WinUIElement under,
		WinElement root,
		UiResolvedInputTarget resolved)
	{
		if (part is UiControlPart)
			return view is null || ReferenceEquals(under, view) ? root : view;

		var bounds = resolved.BoundsInSurfaceDip;
		var ceiling = under;

		for (var current = under; current is not null; current = MauiPlatform.Parent(current))
		{
			if (current is not WinElement element)
				continue;

			// A pixel of slack: the rectangle was measured on the same control, and an ancestor that is the
			// same size as the part is still the part as far as a click goes.
			if (element.ActualWidth > bounds.Width + 1 || element.ActualHeight > bounds.Height + 1)
				break;

			ceiling = element;
		}

		return ceiling;
	}

	private static void Click(Window window, WinUIElement under, WinUIElement ceiling, MauiPoint point, UiClickAction click)
	{
		var chain = new List<WinUIElement>();

		for (var current = under; current is not null; current = MauiPlatform.Parent(current))
		{
			chain.Add(current);

			if (ReferenceEquals(current, ceiling))
				break;
		}

		var drawnAs = MauiPlatform.ElementsDrawnAs(window, chain);
		var count = Math.Max(1, click.Count);

		if (click.Button == UiPointerButtons.Right)
		{
			Menu(window, chain, drawnAs, point, count);
			return;
		}

		// A recognizer waiting for that many taps in a row takes them as one gesture; anything else is
		// clicked as many times as it was asked.
		foreach (var current in chain)
		{
			if (count > 1 && drawnAs.TryGetValue(current, out var owner) && owner is View view && MauiGestures.TryTap(view, click.Button, count, Position(point)))
				return;
		}

		for (var index = 0; index < count; index++)
			ClickOnce(chain, drawnAs, point);
	}

	private static void ClickOnce(List<WinUIElement> chain, Dictionary<WinUIElement, Element> drawnAs, MauiPoint point)
	{
		foreach (var current in chain)
		{
			if (drawnAs.TryGetValue(current, out var owner) && owner is View view && MauiGestures.TryTap(view, UiPointerButtons.Left, 1, Position(point)))
				return;

			if (MauiPlatform.Operate(current))
				return;

			// A click on a field puts the keyboard there and does nothing else.
			if (MauiPlatform.IsTextField(current))
			{
				MauiPlatform.Focus(current);
				return;
			}
		}
	}

	// The secondary button opens what is offered for it: a recognizer that waits for it, or the menu the
	// control carries.
	private static void Menu(Window window, List<WinUIElement> chain, Dictionary<WinUIElement, Element> drawnAs, MauiPoint point, int count)
	{
		foreach (var current in chain)
		{
			if (drawnAs.TryGetValue(current, out var owner) && owner is View view && MauiGestures.TryTap(view, UiPointerButtons.Right, count, Position(point)))
				return;

			if (current.ContextFlyout is { } menu)
			{
				var origin = MauiPlatform.BoundsInWindow(current as WinElement);

				menu.ShowAt(current, new WinFlyoutShowOptions
				{
					Position = origin is { } bounds ? new WinPoint(point.X - bounds.X, point.Y - bounds.Y) : null,
				});

				return;
			}
		}
	}

	// The field text goes into: the node itself, or - for a node that is a whole form - the field at the point.
	private static InputView TextField(Element element, Window window, IReadOnlyList<WinUIElement> hits)
	{
		if (element is InputView field)
			return field;

		var drawnAs = MauiPlatform.ElementsDrawnAs(window, hits.SelectMany(Ancestors));

		foreach (var hit in hits.SelectMany(Ancestors))
		{
			if (drawnAs.TryGetValue(hit, out var owner) && owner is InputView inside)
				return inside;
		}

		throw UiErrors.Fail(UiErrorCodes.NotInteractable, "That node takes no text.");
	}

	private static IEnumerable<WinUIElement> Ancestors(WinUIElement element)
	{
		for (var current = element; current is not null; current = MauiPlatform.Parent(current))
			yield return current;
	}

	// Where the tap landed, asked relative to an element or, with none named, to the window - the same question
	// a recognizer asks of a person's tap.
	private static Func<IElement, MauiPoint?> Position(MauiPoint point)
		=> relativeTo =>
		{
			if (relativeTo is null)
				return point;

			if (relativeTo is Element element && MauiPlatform.ViewOf(element) is { } view && MauiPlatform.BoundsInWindow(view) is { } bounds)
				return new MauiPoint(point.X - bounds.X, point.Y - bounds.Y);

			return null;
		};
}
