namespace StockSharp.DesktopDriver.Tests;

using System.Text.Json;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Serialization;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// The difference between a value that is nothing and a value nobody could read.
/// </summary>
[TestClass]
public class FieldStatusTests : BaseTestClass
{
	[TestMethod]
	public void AKnownNothingIsNotTheSameAsUnreadable()
	{
		var known = UiField<UiValue>.Known(UiNullValue.Instance);
		var unavailable = UiField<UiValue>.Unavailable(UiUnavailableReasons.NotCreated, "the panel was never opened");

		var knownJson = UiJson.Write(known);
		var unavailableJson = UiJson.Write(unavailable);

		AreNotEqual(knownJson, unavailableJson);
		IsTrue(knownJson.Contains("\"known\""), knownJson);
		IsTrue(unavailableJson.Contains("\"notCreated\""), unavailableJson);
		IsTrue(unavailableJson.Contains("the panel was never opened"), unavailableJson);
	}

	[TestMethod]
	public void AFieldComesBackAsWhatItWas()
	{
		var known = UiField<string>.Known("abc");
		var unavailable = UiField<string>.Unavailable(UiUnavailableReasons.Unsupported, null);

		AreEqual(known, UiJson.Read<UiField<string>>(UiJson.Write(known)));
		AreEqual(unavailable, UiJson.Read<UiField<string>>(UiJson.Write(unavailable)));
	}

	[TestMethod]
	public void AFieldWithoutAStatusIsRefused()
	{
		Throws<JsonException>(() => UiJson.Read<UiField<string>>("{\"value\":\"abc\"}"));
	}

	[TestMethod]
	public void AnUnreadableFieldCarryingAValueIsRefused()
	{
		Throws<JsonException>(() => UiJson.Read<UiField<string>>("{\"status\":\"unsupported\",\"value\":\"abc\"}"));
	}

	[TestMethod]
	public void AStatusThisVersionDoesNotKnowIsRefused()
	{
		Throws<JsonException>(() => UiJson.Read<UiField<string>>("{\"status\":\"someFutureStatus\"}"));
	}

	[TestMethod]
	public void AnIndeterminateToggleIsAKnownValue()
	{
		var field = UiField<bool?>.Known(null);

		var restored = UiJson.Read<UiField<bool?>>(UiJson.Write(field));

		IsTrue(restored.IsKnown);
		AreEqual(field, restored);
	}
}
