namespace StockSharp.DesktopDriver.Values;

using System.Text.Json.Serialization;

using StockSharp.DesktopDriver.Serialization;

/// <summary>
/// A field that either carries a value or says why it does not.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
/// <remarks>
/// The protocol avoids the one answer that cannot be checked: a default standing in for a value nobody
/// could read. A test that asks a control with no sorting for its sort direction is told exactly that,
/// rather than "ascending".
/// </remarks>
[JsonConverter(typeof(UiFieldConverterFactory))]
public abstract record UiField<T>
{
	/// <summary>
	/// Whether the field carries a value.
	/// </summary>
	public abstract bool IsKnown { get; }

	/// <summary>
	/// A field carrying a value that was actually read.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>The field.</returns>
	public static UiField<T> Known(T value) => new UiKnown<T>(value);

	/// <summary>
	/// A field that carries no value.
	/// </summary>
	/// <param name="reason">Why there is no value.</param>
	/// <param name="detail">What exactly was in the way.</param>
	/// <returns>The field.</returns>
	public static UiField<T> Unavailable(UiUnavailableReasons reason, string detail = null)
		=> new UiUnavailable<T>(reason, detail);
}

/// <summary>
/// A field carrying a value that was actually read.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="Value">The value.</param>
public sealed record UiKnown<T>(T Value) : UiField<T>
{
	/// <inheritdoc />
	public override bool IsKnown => true;
}

/// <summary>
/// A field that carries no value, and why.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="Reason">Why there is no value.</param>
/// <param name="Detail">What exactly was in the way.</param>
public sealed record UiUnavailable<T>(UiUnavailableReasons Reason, string Detail) : UiField<T>
{
	/// <inheritdoc />
	public override bool IsKnown => false;
}
