namespace StockSharp.DesktopDriver.Input;

using System.Text.Json.Serialization;

/// <summary>
/// What the user would do.
/// </summary>
/// <remarks>
/// These are real input. Nothing here calls a command, sets a property or raises an event: an interface
/// that only works when it is driven from the inside has not been tested at all.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(UiClickAction), UiClickAction.KindName)]
[JsonDerivedType(typeof(UiTextAction), UiTextAction.KindName)]
[JsonDerivedType(typeof(UiKeyAction), UiKeyAction.KindName)]
[JsonDerivedType(typeof(UiScrollAction), UiScrollAction.KindName)]
[JsonDerivedType(typeof(UiEnsureVisibleAction), UiEnsureVisibleAction.KindName)]
public abstract record UiInputAction
{
	/// <summary>
	/// The registered wire name of this action shape.
	/// </summary>
	public abstract string Kind { get; }
}

/// <summary>A press and release of a pointer button.</summary>
/// <param name="Button">Which button.</param>
/// <param name="Count">One click, or two for a double click.</param>
public sealed record UiClickAction(UiPointerButtons Button, int Count) : UiInputAction
{
	/// <summary>The wire name.</summary>
	public const string KindName = "click";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>Typing text.</summary>
/// <param name="Text">What to type.</param>
/// <param name="Mode">Whether to add to what is there or replace it.</param>
public sealed record UiTextAction(string Text, UiTextModes Mode) : UiInputAction
{
	/// <summary>The wire name.</summary>
	public const string KindName = "text";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>A press and release of one key.</summary>
/// <param name="Key">The registered key name.</param>
/// <param name="Modifiers">The modifiers held while it is pressed.</param>
/// <remarks>
/// Always a press and a release: the protocol never leaves a key down between requests, because a test
/// that failed in the middle would leave the desktop holding Shift.
/// </remarks>
public sealed record UiKeyAction(string Key, UiKeyModifiers Modifiers) : UiInputAction
{
	/// <summary>The wire name.</summary>
	public const string KindName = "key";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>Turning the wheel.</summary>
/// <param name="DeltaX">Horizontal notches.</param>
/// <param name="DeltaY">Vertical notches.</param>
/// <remarks>
/// Counted in wheel notches rather than pixels: how far a notch scrolls is the application's decision,
/// and a test that asked for pixels would be asserting on that decision by accident.
/// </remarks>
public sealed record UiScrollAction(double DeltaX, double DeltaY) : UiInputAction
{
	/// <summary>The wire name.</summary>
	public const string KindName = "scroll";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>Bringing a part onto the screen so that it can be acted on.</summary>
/// <remarks>
/// The one thing here that is not input. A grid builds visuals only for what it is showing, so a row a
/// thousand down has no place on screen to click; asking for ten thousand visuals instead would make
/// every such test a memory test. The caller asks for this explicitly and then finds the part again by
/// its key, because the position it had is not the position it has now.
/// <para>
/// It moves the view through the control's own positioning rather than by driving the scrollbar, so a
/// test that used it has not established that scrolling works - only that the part is reachable. A test
/// about scrolling itself sends a scroll.
/// </para>
/// </remarks>
public sealed record UiEnsureVisibleAction : UiInputAction
{
	/// <summary>The wire name.</summary>
	public const string KindName = "ensureVisible";

	/// <summary>The single instance.</summary>
	public static UiEnsureVisibleAction Instance { get; } = new();

	/// <inheritdoc />
	public override string Kind => KindName;
}
