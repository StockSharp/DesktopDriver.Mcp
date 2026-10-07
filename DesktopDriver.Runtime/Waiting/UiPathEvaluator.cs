namespace StockSharp.DesktopDriver.Runtime;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// Reads one field of a snapshot by its wire path, such as <c>state.sorts[0].direction</c>.
/// </summary>
/// <remarks>
/// It walks the snapshot as it goes over the wire, not the objects behind it. That is deliberate: the
/// path a test writes is then exactly the name it sees in a reply, and nothing here can reach a member
/// the protocol does not publish.
/// </remarks>
public static class UiPathEvaluator
{
	private static readonly HashSet<string> _roots = new(StringComparer.Ordinal)
	{
		"node", "stamp", "presentation", "ready", "capabilities", "state", "completeness", "warnings",
	};

	/// <summary>
	/// Reads a path out of a serialised snapshot.
	/// </summary>
	/// <param name="snapshot">The snapshot as JSON.</param>
	/// <param name="path">The wire path.</param>
	/// <param name="value">What was there.</param>
	/// <returns><see langword="true"/> when the path led to a value.</returns>
	/// <remarks>
	/// A path the schema has no root for is a mistake in the request. A path that is well formed but
	/// leads nowhere right now - an index past the end of an array, a field that is unavailable - is not:
	/// it is a value that has not appeared yet, which is usually what the caller is waiting for.
	/// </remarks>
	public static bool TryRead(JsonNode snapshot, string path, out UiValue value)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		ArgumentException.ThrowIfNullOrEmpty(path);

		var segments = Parse(path);

		if (!_roots.Contains(segments[0].Name))
			throw UiErrors.Invalid($"'{path}' does not start at anything a snapshot has.");

		var current = snapshot;
		value = null;

		foreach (var segment in segments)
		{
			if (current is null)
				return false;

			if (segment.Name.Length > 0)
			{
				if (current is not JsonObject obj || !obj.TryGetPropertyValue(segment.Name, out current))
					return false;

				// A field says whether it holds anything; asking past it means asking for its value.
				if (current is JsonObject field && field.ContainsKey("status"))
				{
					if ((string)field["status"] != "known")
						return false;

					current = field["value"];
				}
			}

			foreach (var index in segment.Indices)
			{
				if (current is not JsonArray array || index < 0 || index >= array.Count)
					return false;

				current = array[index];
			}
		}

		value = ToValue(current);

		return value is not null;
	}

	private static UiValue ToValue(JsonNode node)
	{
		switch (node)
		{
			case null:
				return UiNullValue.Instance;
			case JsonObject obj when obj.TryGetPropertyValue("kind", out var kind):
			{
				// A typed value travels with its kind, so it is read back as the type it was written as.
				var name = (string)kind;

				return name switch
				{
					UiValueKinds.Null => UiNullValue.Instance,
					UiValueKinds.Boolean => new UiBooleanValue((bool)obj["value"]),
					UiValueKinds.Int64 => new UiInt64Value(long.Parse((string)obj["value"], CultureInfo.InvariantCulture)),
					UiValueKinds.Decimal => new UiDecimalValue(decimal.Parse((string)obj["value"], NumberStyles.Float, CultureInfo.InvariantCulture)),
					UiValueKinds.Double => new UiDoubleValue((double)obj["value"]),
					UiValueKinds.String => new UiStringValue((string)obj["value"]),
					UiValueKinds.Timestamp => new UiTimestampValue(DateTime.Parse((string)obj["value"], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)),
					_ => null,
				};
			}
			case JsonValue value:
			{
				if (value.TryGetValue<bool>(out var flag))
					return new UiBooleanValue(flag);

				if (value.TryGetValue<long>(out var whole))
					return new UiInt64Value(whole);

				if (value.TryGetValue<double>(out var real))
					return new UiDoubleValue(real);

				if (value.TryGetValue<string>(out var text))
					return new UiStringValue(text);

				return null;
			}
			default:
				return null;
		}
	}

	private static List<Segment> Parse(string path)
	{
		var segments = new List<Segment>();

		foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
		{
			var name = part;
			var indices = new List<int>();

			while (name.EndsWith(']'))
			{
				var open = name.LastIndexOf('[');

				if (open < 0)
					throw UiErrors.Invalid($"'{path}' is not a path.");

				var text = name[(open + 1)..^1];

				if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
					throw UiErrors.Invalid($"'{text}' in '{path}' is not an index.");

				indices.Insert(0, index);
				name = name[..open];
			}

			segments.Add(new Segment(name, indices));
		}

		if (segments.Count == 0)
			throw UiErrors.Invalid("An empty path leads nowhere.");

		return segments;
	}

	private readonly record struct Segment(string Name, List<int> Indices);
}
