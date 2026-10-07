namespace StockSharp.DesktopDriver.Tests;

using System;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Serialization;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// What a value read out of the interface survives being sent as.
/// </summary>
[TestClass]
public class ValueRoundTripTests : BaseTestClass
{
	[TestMethod]
	public void APriceComesBackAsThePriceThatWasSent()
	{
		// Through a JSON number this arrives as 12345678901234.568: a price that has been near a double
		// is no longer the price that was on screen.
		var value = new UiDecimalValue(12345678901234.5678m);

		var json = UiJson.Write<UiValue>(value);
		var restored = (UiDecimalValue)UiJson.Read<UiValue>(json);

		AreEqual(value.Value, restored.Value);
		IsTrue(json.Contains("\"12345678901234.5678\""), $"The decimal did not travel as a string: {json}");
	}

	[TestMethod]
	public void ACountBeyondWhatADoubleHoldsExactlyComesBackWhole()
	{
		var value = new UiInt64Value(9007199254740993L);

		var restored = (UiInt64Value)UiJson.Read<UiValue>(UiJson.Write<UiValue>(value));

		AreEqual(value.Value, restored.Value);
	}

	[TestMethod]
	public void AMomentTravelsInUtcAndComesBackInUtc()
	{
		var value = new UiTimestampValue(new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc));

		var json = UiJson.Write<UiValue>(value);
		var restored = (UiTimestampValue)UiJson.Read<UiValue>(json);

		AreEqual(value.Value, restored.Value);
		AreEqual(DateTimeKind.Utc, restored.Value.Kind);
		IsTrue(json.Contains("2026-01-02T10:00:00"), $"The moment is not ISO 8601: {json}");
	}

	[TestMethod]
	public void EveryKindOfValueSurvivesTheTrip()
	{
		UiValue[] values =
		[
			UiNullValue.Instance,
			new UiBooleanValue(true),
			new UiInt64Value(-42),
			new UiDecimalValue(0.1m),
			new UiDoubleValue(1.5),
			new UiStringValue("текст"),
			new UiTimestampValue(new DateTime(2026, 9, 17, 8, 30, 0, DateTimeKind.Utc)),
		];

		foreach (var value in values)
		{
			var restored = UiJson.Read<UiValue>(UiJson.Write(value));

			AreEqual(value, restored);
			AreEqual(value.Kind, restored.Kind);
		}
	}

	[TestMethod]
	public void TheKindIsWrittenOnceAndNotAlsoAsAProperty()
	{
		// The kind is what the reader dispatches on. Carrying it twice invites the two to disagree.
		var json = UiJson.Write<UiValue>(new UiStringValue("a"));

		AreEqual(1, CountOccurrences(json, "\"kind\""), json);
	}

	[TestMethod]
	public void AnInfinityIsNotAValue()
	{
		Throws<ArgumentException>(() => UiJson.Write<UiValue>(new UiDoubleValue(double.PositiveInfinity)));
	}

	private static int CountOccurrences(string text, string token)
	{
		var count = 0;
		var index = 0;

		while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
		{
			count++;
			index += token.Length;
		}

		return count;
	}
}
