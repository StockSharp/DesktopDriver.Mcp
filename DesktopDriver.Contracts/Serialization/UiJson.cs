namespace StockSharp.DesktopDriver.Serialization;

using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using StockSharp.DesktopDriver.States;

/// <summary>
/// How everything in this protocol is written down.
/// </summary>
/// <remarks>
/// One set of options, shared by the host, the client, the command line and the agent tools. A second set
/// would be a second protocol: the two would agree until the day one of them was changed.
/// </remarks>
public static class UiJson
{
	/// <summary>
	/// The protocol's schema version.
	/// </summary>
	public const string SchemaVersion = "1.0";

	/// <summary>
	/// The options every side of the protocol uses.
	/// </summary>
	public static JsonSerializerOptions Options { get; } = Create();

	/// <summary>
	/// Writes a value the way the protocol writes it.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="value">The value.</param>
	/// <returns>The JSON.</returns>
	public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

	/// <summary>
	/// Reads a value the way the protocol writes it.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="json">The JSON.</param>
	/// <returns>The value.</returns>
	public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);

	private static JsonSerializerOptions Create()
	{
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
			NumberHandling = JsonNumberHandling.Strict,
			TypeInfoResolver = new DefaultJsonTypeInfoResolver
			{
				Modifiers = { DropDiscriminatorProperty, AddRegisteredStates },
			},
		};

		options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
		options.Converters.Add(new UiInt64Converter());
		options.Converters.Add(new UiDecimalConverter());
		options.Converters.Add(new UiDateTimeConverter());
		options.Converters.Add(new UiFieldConverterFactory());

		return options;
	}

	// A polymorphic type says its kind twice otherwise: once as the discriminator the reader dispatches
	// on, once as the property the record declares. Duplicated keys are refused by the protocol, and the
	// two could disagree. Only types inside such a hierarchy are touched: elsewhere - on a node reference,
	// for instance - "kind" is an ordinary member and removing it would leave its constructor unbindable.
	private static void DropDiscriminatorProperty(JsonTypeInfo typeInfo)
	{
		if (typeInfo.Kind != JsonTypeInfoKind.Object || !IsPolymorphic(typeInfo.Type))
			return;

		var property = typeInfo.Properties.FirstOrDefault(item => item.Name == "kind");

		if (property is not null)
			typeInfo.Properties.Remove(property);
	}

	private static bool IsPolymorphic(Type type)
	{
		for (var current = type; current is not null; current = current.BaseType)
		{
			if (current.GetCustomAttribute<JsonPolymorphicAttribute>(false) is not null)
				return true;
		}

		return false;
	}

	// State shapes come from the layer that owns the control, so they cannot be listed on the base type
	// in this assembly. They are added to its polymorphism options as they register themselves.
	private static void AddRegisteredStates(JsonTypeInfo typeInfo)
	{
		if (typeInfo.Type != typeof(UiState))
			return;

		typeInfo.PolymorphismOptions ??= new JsonPolymorphismOptions
		{
			TypeDiscriminatorPropertyName = "kind",
			IgnoreUnrecognizedTypeDiscriminators = false,
			UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
		};

		foreach (var (kind, type) in UiStateRegistry.Registered)
		{
			if (!typeInfo.PolymorphismOptions.DerivedTypes.Any(derived => derived.DerivedType == type))
				typeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(type, kind));
		}
	}
}
