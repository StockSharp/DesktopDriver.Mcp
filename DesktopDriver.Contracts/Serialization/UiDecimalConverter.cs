namespace StockSharp.DesktopDriver.Serialization;

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Writes decimals as strings.
/// </summary>
/// <remarks>
/// A price that goes out as a JSON number comes back through a double on most readers, and a price that
/// has been through a double is no longer the price that was on screen.
/// </remarks>
public sealed class UiDecimalConverter : JsonConverter<decimal>
{
	/// <inheritdoc />
	public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		=> reader.TokenType switch
		{
			JsonTokenType.String => decimal.Parse(reader.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture),
			JsonTokenType.Number => reader.GetDecimal(),
			_ => throw new JsonException("A decimal travels as a string."),
		};

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);

		writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
	}
}
