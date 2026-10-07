namespace StockSharp.DesktopDriver.Runtime;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

/// <summary>
/// Gives each row of a table a name that stays with it.
/// </summary>
/// <remarks>
/// A row is addressed by key and never by position, because position is the one thing sorting, filtering
/// and grouping all change - and those are exactly what a test about a table is usually checking. The key
/// belongs to the record, so the same record keeps it after the table is re-sorted.
/// <para>
/// Held weakly: a table that names ten thousand rows must not be the reason they are still in memory when
/// the panel showing them has been closed.
/// </para>
/// <para>
/// Kept here rather than beside either interface toolkit's table, because the bargain is the same on both
/// and a case that reads a key from one and writes it back to the other must mean the same row by it.
/// </para>
/// </remarks>
public sealed class UiRowKeys
{
	private static readonly ConditionalWeakTable<object, UiRowKeys> _byOwner = [];

	private readonly ConditionalWeakTable<object, string> _keys = [];
	private readonly Dictionary<string, WeakReference<object>> _items = new(StringComparer.Ordinal);
	private readonly Lock _sync = new();

	private long _next;

	/// <summary>
	/// The keys of one table.
	/// </summary>
	/// <param name="owner">The table.</param>
	/// <returns>Its keys.</returns>
	public static UiRowKeys For(object owner)
	{
		ArgumentNullException.ThrowIfNull(owner);

		return _byOwner.GetValue(owner, _ => new UiRowKeys());
	}

	/// <summary>
	/// The key of a record, issuing one if it has none yet.
	/// </summary>
	/// <param name="item">The record.</param>
	/// <returns>Its key.</returns>
	public string KeyOf(object item)
	{
		if (item is null)
			return null;

		using (_sync.EnterScope())
		{
			if (_keys.TryGetValue(item, out var existing))
				return existing;

			var key = $"row-{Interlocked.Increment(ref _next)}";

			_keys.Add(item, key);
			_items[key] = new WeakReference<object>(item);

			return key;
		}
	}

	/// <summary>
	/// The record a key names, when it is still there.
	/// </summary>
	/// <param name="key">The key.</param>
	/// <param name="item">The record.</param>
	/// <returns><see langword="true"/> when the record is still there.</returns>
	public bool TryResolve(string key, out object item)
	{
		item = null;

		if (string.IsNullOrEmpty(key))
			return false;

		using (_sync.EnterScope())
			return _items.TryGetValue(key, out var reference) && reference.TryGetTarget(out item);
	}
}
