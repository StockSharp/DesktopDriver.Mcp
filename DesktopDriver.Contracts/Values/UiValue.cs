namespace StockSharp.DesktopDriver.Values;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// One value read out of the interface, with its type preserved.
/// </summary>
/// <remarks>
/// A price compared as a string passes when the control formats it differently and fails when the locale
/// changes, so values travel typed. Decimals stay decimal for the same reason: a price that survives the
/// trip as a double is no longer the price that was on screen.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(UiNullValue), UiValueKinds.Null)]
[JsonDerivedType(typeof(UiBooleanValue), UiValueKinds.Boolean)]
[JsonDerivedType(typeof(UiInt64Value), UiValueKinds.Int64)]
[JsonDerivedType(typeof(UiDecimalValue), UiValueKinds.Decimal)]
[JsonDerivedType(typeof(UiDoubleValue), UiValueKinds.Double)]
[JsonDerivedType(typeof(UiStringValue), UiValueKinds.String)]
[JsonDerivedType(typeof(UiTimestampValue), UiValueKinds.Timestamp)]
public abstract record UiValue
{
	/// <summary>
	/// The registered wire name of this kind of value.
	/// </summary>
	public abstract string Kind { get; }
}

/// <summary>
/// A value that is known to be nothing.
/// </summary>
public sealed record UiNullValue : UiValue
{
	/// <summary>The single instance.</summary>
	public static UiNullValue Instance { get; } = new();

	/// <inheritdoc />
	public override string Kind => UiValueKinds.Null;
}

/// <summary>
/// A true or false value.
/// </summary>
/// <param name="Value">The value.</param>
public sealed record UiBooleanValue(bool Value) : UiValue
{
	/// <inheritdoc />
	public override string Kind => UiValueKinds.Boolean;
}

/// <summary>
/// A whole number.
/// </summary>
/// <param name="Value">The value.</param>
public sealed record UiInt64Value(long Value) : UiValue
{
	/// <inheritdoc />
	public override string Kind => UiValueKinds.Int64;
}

/// <summary>
/// An exact decimal number, such as a price or a volume.
/// </summary>
/// <param name="Value">The value.</param>
public sealed record UiDecimalValue(decimal Value) : UiValue
{
	/// <inheritdoc />
	public override string Kind => UiValueKinds.Decimal;
}

/// <summary>
/// A floating point number. Infinities and NaN are not values.
/// </summary>
/// <param name="Value">The value.</param>
public sealed record UiDoubleValue(double Value) : UiValue
{
	/// <inheritdoc />
	public override string Kind => UiValueKinds.Double;
}

/// <summary>
/// Text.
/// </summary>
/// <param name="Value">The value.</param>
public sealed record UiStringValue(string Value) : UiValue
{
	/// <inheritdoc />
	public override string Kind => UiValueKinds.String;
}

/// <summary>
/// A moment in time, always UTC.
/// </summary>
/// <param name="Value">The value.</param>
public sealed record UiTimestampValue(DateTime Value) : UiValue
{
	/// <inheritdoc />
	public override string Kind => UiValueKinds.Timestamp;
}
