namespace StockSharp.DesktopDriver.Runtime;

using System;
using System.Text.Json.Nodes;

using StockSharp.DesktopDriver.Serialization;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;
using StockSharp.DesktopDriver.Waiting;

/// <summary>
/// Whether a condition holds, does not hold, or cannot be told yet.
/// </summary>
public enum UiConditionResults
{
	/// <summary>It holds.</summary>
	True,

	/// <summary>It does not.</summary>
	False,

	/// <summary>Nothing can be said: the value is not there to compare.</summary>
	Unavailable,
}

/// <summary>
/// Decides whether what is on screen satisfies what was asked for.
/// </summary>
/// <remarks>
/// Three outcomes, not two. A field that cannot be read yet is not "not equal": a wait for it should
/// keep waiting, and a wait for its opposite should not finish early because the value was missing.
/// </remarks>
public static class UiConditionEvaluator
{
	private const int _maxTerms = 16;
	private const int _maxDepth = 4;

	/// <summary>
	/// Checks a condition against a node.
	/// </summary>
	/// <param name="condition">What was asked for.</param>
	/// <param name="snapshot">The node, or <see langword="null"/> when it does not exist.</param>
	/// <returns>Whether it holds.</returns>
	public static UiConditionResults Evaluate(UiCondition condition, UiNodeSnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(condition);

		Validate(condition, 0, 0);

		var json = snapshot is null ? null : JsonNode.Parse(UiJson.Write(snapshot));

		return Evaluate(condition, snapshot, json);
	}

	private static UiConditionResults Evaluate(UiCondition condition, UiNodeSnapshot snapshot, JsonNode json)
	{
		switch (condition)
		{
			case UiExistsCondition exists:
				return (snapshot is not null) == exists.Expected ? UiConditionResults.True : UiConditionResults.False;

			case UiFieldCondition field:
			{
				if (json is null)
					return UiConditionResults.Unavailable;

				return !UiPathEvaluator.TryRead(json, field.Path, out var actual)
					? UiConditionResults.Unavailable
					: Compare(actual, field.Comparison, field.Expected);
			}

			case UiRevisionAfterCondition revision:
			{
				if (snapshot is null)
					return UiConditionResults.Unavailable;

				var current = revision.RevisionKind switch
				{
					UiRevisionKinds.State => snapshot.Stamp.Revisions.State,
					UiRevisionKinds.View => snapshot.Stamp.Revisions.View,
					UiRevisionKinds.Layout => snapshot.Stamp.Revisions.Layout,
					_ => throw new ArgumentOutOfRangeException(nameof(condition)),
				};

				if (current.Epoch != revision.Baseline.Epoch)
				{
					throw UiErrors.Changed(
						"This node's source was replaced, so its versions cannot be compared with the caller's baseline.");
				}

				return current.Version > revision.Baseline.Version ? UiConditionResults.True : UiConditionResults.False;
			}

			case UiAllCondition all:
			{
				var result = UiConditionResults.True;

				foreach (var item in all.Conditions)
				{
					var one = Evaluate(item, snapshot, json);

					if (one == UiConditionResults.False)
						return UiConditionResults.False;

					if (one == UiConditionResults.Unavailable)
						result = UiConditionResults.Unavailable;
				}

				return result;
			}

			case UiAnyCondition any:
			{
				var result = UiConditionResults.False;

				foreach (var item in any.Conditions)
				{
					var one = Evaluate(item, snapshot, json);

					if (one == UiConditionResults.True)
						return UiConditionResults.True;

					if (one == UiConditionResults.Unavailable)
						result = UiConditionResults.Unavailable;
				}

				return result;
			}

			default:
				throw UiErrors.Invalid($"{condition.Kind} is not a condition this version knows.");
		}
	}

	private static UiConditionResults Compare(UiValue actual, UiComparisons comparison, UiValue expected)
	{
		if (actual is UiNullValue || expected is null)
			return comparison == UiComparisons.NotEqual && actual is UiNullValue && expected is not null
				? UiConditionResults.True
				: UiConditionResults.Unavailable;

		if (comparison is UiComparisons.Equal or UiComparisons.NotEqual)
		{
			var equal = actual.Equals(expected);

			return (comparison == UiComparisons.Equal) == equal ? UiConditionResults.True : UiConditionResults.False;
		}

		if (comparison == UiComparisons.Contains)
		{
			if (actual is not UiStringValue text || expected is not UiStringValue part)
				return UiConditionResults.Unavailable;

			return text.Value is not null && part.Value is not null && text.Value.Contains(part.Value, StringComparison.Ordinal)
				? UiConditionResults.True
				: UiConditionResults.False;
		}

		if (!TryNumber(actual, out var left) || !TryNumber(expected, out var right))
			return UiConditionResults.Unavailable;

		var sign = left.CompareTo(right);

		var holds = comparison switch
		{
			UiComparisons.Greater => sign > 0,
			UiComparisons.GreaterOrEqual => sign >= 0,
			UiComparisons.Less => sign < 0,
			UiComparisons.LessOrEqual => sign <= 0,
			_ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
		};

		return holds ? UiConditionResults.True : UiConditionResults.False;
	}

	// Compared as decimal so that a price is compared as the price it is: through a double, two prices
	// that differ in the last digit can compare equal.
	private static bool TryNumber(UiValue value, out decimal number)
	{
		switch (value)
		{
			case UiDecimalValue amount:
				number = amount.Value;
				return true;
			case UiInt64Value whole:
				number = whole.Value;
				return true;
			case UiDoubleValue real:
				number = (decimal)real.Value;
				return true;
			default:
				number = 0;
				return false;
		}
	}

	private static int Validate(UiCondition condition, int depth, int terms)
	{
		if (depth > _maxDepth)
			throw UiErrors.Invalid($"A condition may not nest deeper than {_maxDepth}.");

		switch (condition)
		{
			case UiAllCondition all:
			{
				if (all.Conditions.IsDefaultOrEmpty)
					throw UiErrors.Invalid("An empty composite condition says nothing.");

				foreach (var item in all.Conditions)
					terms = Validate(item, depth + 1, terms);

				break;
			}
			case UiAnyCondition any:
			{
				if (any.Conditions.IsDefaultOrEmpty)
					throw UiErrors.Invalid("An empty composite condition says nothing.");

				foreach (var item in any.Conditions)
					terms = Validate(item, depth + 1, terms);

				break;
			}
			default:
				terms++;
				break;
		}

		if (terms > _maxTerms)
			throw UiErrors.Invalid($"A condition may not have more than {_maxTerms} terms.");

		return terms;
	}
}
