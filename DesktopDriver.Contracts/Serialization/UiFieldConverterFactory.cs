namespace StockSharp.DesktopDriver.Serialization;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// Writes a field as its status and, when there is one, its value.
/// </summary>
/// <remarks>
/// The status is never left out, so a reader can tell "known to be nothing" from "nobody could read it"
/// without guessing from whether a property is present.
/// </remarks>
public sealed class UiFieldConverterFactory : JsonConverterFactory
{
	/// <inheritdoc />
	public override bool CanConvert(Type typeToConvert)
		=> typeToConvert.IsGenericType &&
			typeToConvert.GetGenericTypeDefinition() == typeof(UiField<>);

	/// <inheritdoc />
	public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
	{
		var valueType = typeToConvert.GetGenericArguments()[0];
		var converterType = typeof(UiFieldConverter<>).MakeGenericType(valueType);

		return (JsonConverter)Activator.CreateInstance(converterType);
	}

	private sealed class UiFieldConverter<T> : JsonConverter<UiField<T>>
	{
		private const string _statusName = "status";
		private const string _valueName = "value";
		private const string _detailName = "detail";
		private const string _knownStatus = "known";

		public override UiField<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.StartObject)
				throw new JsonException("A field is an object carrying its status.");

			var status = (string)null;
			var detail = (string)null;
			var value = default(T);
			var hasValue = false;

			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndObject)
					break;

				if (reader.TokenType != JsonTokenType.PropertyName)
					throw new JsonException("A field carries named members only.");

				var name = reader.GetString();
				reader.Read();

				switch (name)
				{
					case _statusName:
						status = reader.GetString();
						break;
					case _valueName:
						value = JsonSerializer.Deserialize<T>(ref reader, options);
						hasValue = true;
						break;
					case _detailName:
						detail = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
						break;
					default:
						throw new JsonException($"A field has no member '{name}'.");
				}
			}

			if (status is null)
				throw new JsonException("A field must say its status.");

			if (status == _knownStatus)
			{
				if (!hasValue)
					throw new JsonException("A known field must carry its value.");

				return UiField<T>.Known(value);
			}

			if (hasValue)
				throw new JsonException("A field that is not known must not carry a value.");

			return UiField<T>.Unavailable(ParseReason(status), detail);
		}

		public override void Write(Utf8JsonWriter writer, UiField<T> value, JsonSerializerOptions options)
		{
			ArgumentNullException.ThrowIfNull(writer);
			ArgumentNullException.ThrowIfNull(value);

			writer.WriteStartObject();

			if (value is UiKnown<T> known)
			{
				writer.WriteString(_statusName, _knownStatus);
				writer.WritePropertyName(_valueName);
				JsonSerializer.Serialize(writer, known.Value, options);
			}
			else
			{
				var unavailable = (UiUnavailable<T>)value;

				writer.WriteString(_statusName, UiWireNames.ToWire(unavailable.Reason.ToString()));

				if (unavailable.Detail is not null)
					writer.WriteString(_detailName, unavailable.Detail);
			}

			writer.WriteEndObject();
		}

		private static UiUnavailableReasons ParseReason(string status)
		{
			foreach (var reason in Enum.GetValues<UiUnavailableReasons>())
			{
				if (UiWireNames.ToWire(reason.ToString()) == status)
					return reason;
			}

			throw new JsonException($"'{status}' is not a status this protocol version knows.");
		}
	}
}
