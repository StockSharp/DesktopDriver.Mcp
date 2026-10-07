namespace StockSharp.DesktopDriver.States;

using System.Collections.Immutable;
using System.Text.Json.Serialization;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// A description of the filter a control currently has.
/// </summary>
/// <remarks>
/// This describes what is set, it is not a query language: nothing on the other side may execute it. A
/// control whose own criterion cannot be expressed here says so, rather than being summarised into
/// something simpler that a test would then believe.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(UiFieldFilter), UiFieldFilter.KindName)]
[JsonDerivedType(typeof(UiFilterGroup), UiFilterGroup.KindName)]
public abstract record UiFilter
{
	/// <summary>
	/// The registered wire name of this filter shape.
	/// </summary>
	public abstract string Kind { get; }
}

/// <summary>
/// A comparison of one field with one or more arguments.
/// </summary>
/// <param name="FieldId">The field being compared.</param>
/// <param name="Operator">How it is compared.</param>
/// <param name="Arguments">What it is compared with.</param>
public sealed record UiFieldFilter(
	string FieldId,
	UiFilterOperators Operator,
	ImmutableArray<UiValue> Arguments) : UiFilter
{
	/// <summary>The wire name.</summary>
	public const string KindName = "field";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>
/// Several filters combined.
/// </summary>
/// <param name="Operator">How they are combined.</param>
/// <param name="Items">The filters.</param>
public sealed record UiFilterGroup(
	UiBooleanOperators Operator,
	ImmutableArray<UiFilter> Items) : UiFilter
{
	/// <summary>The wire name.</summary>
	public const string KindName = "group";

	/// <summary>
	/// The filter of a control that is not filtering anything.
	/// </summary>
	public static UiFilterGroup Empty { get; } = new(UiBooleanOperators.All, ImmutableArray<UiFilter>.Empty);

	/// <inheritdoc />
	public override string Kind => KindName;
}
