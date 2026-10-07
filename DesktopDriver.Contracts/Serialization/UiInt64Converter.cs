namespace StockSharp.DesktopDriver.Serialization;

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Writes whole numbers as strings.
/// </summary>
/// <remarks>
/// A count of rows can exceed what a double represents exactly, and JSON numbers are read as doubles by
/// most of what will consume this protocol. A string arrives as the number that was sent.
/// </remarks>
public sealed class UiInt64Converter : JsonConverter<long>
{
	/// <inheritdoc />
	public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		=> reader.TokenType switch
		{
			JsonTokenType.String => long.Parse(reader.GetString(), CultureInfo.InvariantCulture),
			JsonTokenType.Number => reader.GetInt64(),
			_ => throw new JsonException("A whole number travels as a string."),
		};

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);

		writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
	}
}
