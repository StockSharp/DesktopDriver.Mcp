namespace StockSharp.DesktopDriver.Tests.Maui;

using System;

using Ecng.UnitTesting;

using Microsoft.Maui.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Maui;

using MauiPoint = Microsoft.Maui.Graphics.Point;

/// <summary>
/// What a click and a key do to a MAUI control.
/// </summary>
[TestClass]
public class InputTests : BaseTestClass
{
	private static (Label Label, Func<int> Commands, Func<int> Taps) Tappable(int taps, ButtonsMask buttons)
	{
		var commands = 0;
		var tapped = 0;
		var recognizer = new TapGestureRecognizer
		{
			NumberOfTapsRequired = taps,
			Buttons = buttons,
			Command = new Command(() => commands++),
		};

		recognizer.Tapped += (_, _) => tapped++;

		var label = new Label { Text = "Trade", GestureRecognizers = { recognizer } };

		return (label, () => commands, () => tapped);
	}

	[TestMethod]
	public void ATapReachesTheRecognizerThatWaitsForIt()
	{
		var (label, commands, taps) = Tappable(1, ButtonsMask.Primary);

		IsTrue(MauiGestures.TryTap(label, UiPointerButtons.Left, 1, _ => new MauiPoint(10, 10)));

		// The command runs and the event is raised, once each, the way a person's tap does both.
		AreEqual(1, commands());
		AreEqual(1, taps());
	}

	[TestMethod]
	public void ADoubleTapIsNotTwoSingleOnes()
	{
		var (label, commands, _) = Tappable(2, ButtonsMask.Primary);

		IsFalse(MauiGestures.TryTap(label, UiPointerButtons.Left, 1, _ => new MauiPoint(10, 10)));
		AreEqual(0, commands());

		IsTrue(MauiGestures.TryTap(label, UiPointerButtons.Left, 2, _ => new MauiPoint(10, 10)));
		AreEqual(1, commands());
	}

	[TestMethod]
	public void TheSecondaryButtonReachesOnlyARecognizerThatAsksForIt()
	{
		var (label, commands, _) = Tappable(1, ButtonsMask.Secondary);

		IsFalse(MauiGestures.TryTap(label, UiPointerButtons.Left, 1, _ => new MauiPoint(10, 10)));
		IsTrue(MauiGestures.TryTap(label, UiPointerButtons.Right, 1, _ => new MauiPoint(10, 10)));
		AreEqual(1, commands());
	}

	[TestMethod]
	public void EnterFinishesAField()
	{
		var entry = new Entry();
		var finished = 0;

		entry.Completed += (_, _) => finished++;

		MauiKeys.Press(entry, "Enter");

		AreEqual(1, finished);
	}

	[TestMethod]
	public void EnterSearches()
	{
		var search = new SearchBar();
		var searched = 0;

		search.SearchButtonPressed += (_, _) => searched++;

		MauiKeys.Press(search, "NumpadEnter");

		AreEqual(1, searched);
	}

	[TestMethod]
	public void AKeyNoFieldAnswersIsRefusedRatherThanDropped()
		=> Throws<UiAutomationException>(() => MauiKeys.Press(new Entry(), "Tab"));
}
