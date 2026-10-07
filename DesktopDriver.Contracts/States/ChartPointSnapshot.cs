namespace StockSharp.DesktopDriver.States;

using System;
using System.Text.Json.Serialization;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// One point of a series.
/// </summary>
/// <param name="Key">The point's stable key.</param>
/// <param name="Index">Its position in the series as the adapter defines the series.</param>
/// <param name="Origin">Where the value was read from.</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(CandlePointSnapshot), CandlePointSnapshot.KindName)]
[JsonDerivedType(typeof(LinePointSnapshot), LinePointSnapshot.KindName)]
public abstract record ChartPointSnapshot(string Key, long Index, UiDataOrigins Origin)
{
	/// <summary>
	/// The registered wire name of this point shape.
	/// </summary>
	public abstract string Kind { get; }
}

/// <summary>
/// One candle.
/// </summary>
/// <param name="Key">The point's stable key.</param>
/// <param name="Index">Its position in the series.</param>
/// <param name="Origin">Where the value was read from.</param>
/// <param name="Time">When the candle opened, UTC.</param>
/// <param name="Open">Opening price.</param>
/// <param name="High">Highest price.</param>
/// <param name="Low">Lowest price.</param>
/// <param name="Close">Closing price.</param>
/// <param name="Volume">Total volume.</param>
/// <param name="IsComplete">Whether the candle has finished.</param>
public sealed record CandlePointSnapshot(
	string Key,
	long Index,
	UiDataOrigins Origin,
	DateTime Time,
	decimal Open,
	decimal High,
	decimal Low,
	decimal Close,
	decimal Volume,
	bool IsComplete) : ChartPointSnapshot(Key, Index, Origin)
{
	/// <summary>The wire name.</summary>
	public const string KindName = "candle";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>
/// One point of a line, which may be a known gap.
/// </summary>
/// <param name="Key">The point's stable key.</param>
/// <param name="Index">Its position in the series.</param>
/// <param name="Origin">Where the value was read from.</param>
/// <param name="X">Its position along the horizontal axis.</param>
/// <param name="Y">Its value, absent when the point is a gap.</param>
/// <param name="IsGap">Whether the series is known to have no value here.</param>
/// <remarks>
/// A gap and a value that could not be read are different: the first is part of the data, the second is a
/// limitation of the reader, and an indicator that has not warmed up yet is the first.
/// </remarks>
public sealed record LinePointSnapshot(
	string Key,
	long Index,
	UiDataOrigins Origin,
	UiValue X,
	UiField<UiValue> Y,
	bool IsGap) : ChartPointSnapshot(Key, Index, Origin)
{
	/// <summary>The wire name.</summary>
	public const string KindName = "line";

	/// <inheritdoc />
	public override string Kind => KindName;
}
