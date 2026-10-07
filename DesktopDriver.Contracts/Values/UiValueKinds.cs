namespace StockSharp.DesktopDriver.Values;

/// <summary>
/// The wire names of the value kinds.
/// </summary>
/// <remarks>
/// They are written down once, here, because they are part of the protocol: a reader on the other side
/// matches these strings, and renaming a CLR type must not change what goes over the wire.
/// </remarks>
public static class UiValueKinds
{
	/// <summary><see cref="UiNullValue"/>.</summary>
	public const string Null = "null";

	/// <summary><see cref="UiBooleanValue"/>.</summary>
	public const string Boolean = "boolean";

	/// <summary><see cref="UiInt64Value"/>.</summary>
	public const string Int64 = "int64";

	/// <summary><see cref="UiDecimalValue"/>.</summary>
	public const string Decimal = "decimal";

	/// <summary><see cref="UiDoubleValue"/>.</summary>
	public const string Double = "double";

	/// <summary><see cref="UiStringValue"/>.</summary>
	public const string String = "string";

	/// <summary><see cref="UiTimestampValue"/>.</summary>
	public const string Timestamp = "timestamp";
}
