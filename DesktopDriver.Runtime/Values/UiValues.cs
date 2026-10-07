namespace StockSharp.DesktopDriver.Runtime;

using System;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// Turns a value read out of a running application into one the protocol carries.
/// </summary>
/// <remarks>
/// Types are kept, not rendered. A price compared as a string passes when the control formats it
/// differently and fails when the machine's locale changes; a decimal that survives the trip as a double
/// is no longer the price that was on screen.
/// </remarks>
public static class UiValues
{
	/// <summary>
	/// The protocol's form of a value.
	/// </summary>
	/// <param name="value">What was read.</param>
	/// <returns>The value.</returns>
	/// <remarks>
	/// Anything with no kind of its own arrives as the text it renders as, which is what a person sees.
	/// Enumerations are the common case and their names mean more than their numbers.
	/// </remarks>
	public static UiValue From(object value) => value switch
	{
		null => UiNullValue.Instance,
		bool flag => new UiBooleanValue(flag),
		byte or sbyte or short or ushort or int or uint or long => new UiInt64Value(Convert.ToInt64(value)),
		ulong number => number <= long.MaxValue ? new UiInt64Value((long)number) : new UiStringValue(number.ToString()),
		decimal amount => new UiDecimalValue(amount),
		float or double => new UiDoubleValue(Convert.ToDouble(value)),
		DateTime moment => new UiTimestampValue(moment.ToUniversalTime()),
		DateTimeOffset moment => new UiTimestampValue(moment.UtcDateTime),
		TimeSpan span => new UiStringValue(span.ToString()),
		string text => new UiStringValue(text),
		Enum name => new UiStringValue(name.ToString()),
		_ => new UiStringValue(value.ToString()),
	};
}
