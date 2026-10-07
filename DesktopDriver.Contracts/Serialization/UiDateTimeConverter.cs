namespace StockSharp.DesktopDriver.Serialization;

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Writes moments as ISO 8601 in UTC.
/// </summary>
/// <remarks>
/// The protocol has one clock. A local time on the wire would be read by whoever receives it as their
/// local time, and the two are the same only on the machine that sent it.
/// </remarks>
public sealed class UiDateTimeConverter : JsonConverter<DateTime>
{
	private const string _format = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

	/// <inheritdoc />
	public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var text = reader.GetString();

		if (!DateTime.TryParse(
			text,
			CultureInfo.InvariantCulture,
			DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
			out var value))
		{
			throw new JsonException($"'{text}' is not a moment in ISO 8601.");
		}

		return DateTime.SpecifyKind(value, DateTimeKind.Utc);
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);

		var utc = value.Kind switch
		{
			DateTimeKind.Utc => value,
			DateTimeKind.Local => value.ToUniversalTime(),
			_ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
		};

		writer.WriteStringValue(utc.ToString(_format, CultureInfo.InvariantCulture));
	}
}
