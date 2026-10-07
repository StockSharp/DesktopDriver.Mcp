namespace StockSharp.DesktopDriver.Maui;

using System;
using System.Linq;
using System.Reflection;

using Microsoft.Maui;
using Microsoft.Maui.Controls;

using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Runtime;

using MauiPoint = Microsoft.Maui.Graphics.Point;

/// <summary>
/// Delivers a tap to the tap recognizers of a MAUI view.
/// </summary>
/// <remarks>
/// A view made clickable with a tap recognizer is not a platform control with an action of its own: MAUI
/// watches the pointer on the platform side and hands the tap to the recognizer itself. That hand-over is
/// internal to MAUI, and it is the one call made here - so the command runs and the event is raised exactly
/// as for a person's tap, with the same argument, rather than by a second copy of MAUI's own rules.
/// </remarks>
internal static class MauiGestures
{
	private static readonly MethodInfo _sendTapped = typeof(TapGestureRecognizer)
		.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
		.FirstOrDefault(method => method.Name == "SendTapped" && method.GetParameters() is [{ ParameterType: var sender }, ..] && sender == typeof(View));

	/// <summary>
	/// Taps a view the way its recognizers ask to be tapped.
	/// </summary>
	/// <param name="view">The view.</param>
	/// <param name="button">The button that was pressed.</param>
	/// <param name="count">How many times in a row.</param>
	/// <param name="position">Where the tap landed relative to an element, or to the window for none.</param>
	/// <returns><see langword="true"/> when one of its recognizers took the tap.</returns>
	public static bool TryTap(View view, UiPointerButtons button, int count, Func<IElement, MauiPoint?> position)
	{
		if (view is null)
			return false;

		var wanted = button switch
		{
			UiPointerButtons.Left => ButtonsMask.Primary,
			UiPointerButtons.Right => ButtonsMask.Secondary,
			_ => (ButtonsMask?)null,
		};

		if (wanted is null)
			return false;

		var taps = view.GestureRecognizers
			.OfType<TapGestureRecognizer>()
			.Where(tap => tap.NumberOfTapsRequired == Math.Max(1, count) && tap.Buttons.HasFlag(wanted.Value))
			.ToArray();

		if (taps.Length == 0)
			return false;

		if (_sendTapped is null)
		{
			throw UiErrors.Unsupported(
				"This version of MAUI no longer hands a tap to its recognizers the way this backend delivers it.");
		}

		foreach (var tap in taps)
		{
			var arguments = _sendTapped.GetParameters().Length switch
			{
				1 => new object[] { view },
				_ => new object[] { view, position },
			};

			_sendTapped.Invoke(tap, arguments);
		}

		return true;
	}
}
