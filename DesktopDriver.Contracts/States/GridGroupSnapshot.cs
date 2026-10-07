namespace StockSharp.DesktopDriver.States;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// One group of a grid.
/// </summary>
/// <param name="Id">The group's identifier.</param>
/// <param name="ParentId">The group it sits inside, when it does.</param>
/// <param name="Key">The value the group collects.</param>
/// <param name="Level">Its nesting level.</param>
/// <param name="IsExpanded">Whether it is open.</param>
/// <param name="DataRowCount">How many records it holds.</param>
/// <param name="Aggregates">Its totals, by column.</param>
public sealed record GridGroupSnapshot(
	string Id,
	string ParentId,
	UiValue Key,
	int Level,
	bool IsExpanded,
	UiField<long> DataRowCount,
	ImmutableDictionary<string, UiField<UiValue>> Aggregates);
