namespace StockSharp.DesktopDriver.Runtime;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;

/// <summary>
/// The last so many things the module noticed.
/// </summary>
/// <remarks>
/// Bounded on purpose, which means it can lose its own beginning. A reader asking from a point that has
/// already been dropped is told so, because a gap nobody mentions reads as a period when nothing
/// happened.
/// </remarks>
public sealed class UiDiagnosticBuffer
{
	private readonly Lock _sync = new();
	private readonly Queue<UiDiagnosticEntry> _entries = new();
	private readonly int _capacity;

	private long _sequence;
	private long _oldestKept = 1;

	/// <summary>
	/// Initializes a new instance of the <see cref="UiDiagnosticBuffer"/> class.
	/// </summary>
	/// <param name="capacity">How many entries to keep.</param>
	public UiDiagnosticBuffer(int capacity = 1000)
	{
		if (capacity <= 0)
			throw new ArgumentOutOfRangeException(nameof(capacity));

		_capacity = capacity;
	}

	/// <summary>
	/// Notes something down.
	/// </summary>
	/// <param name="severity">How much it matters.</param>
	/// <param name="code">What kind of thing it is.</param>
	/// <param name="message">What happened.</param>
	/// <param name="relatedNode">The node it is about.</param>
	/// <param name="relatedAction">The action it is about.</param>
	public void Append(
		string severity,
		string code,
		string message,
		UiNodeId relatedNode = null,
		Guid? relatedAction = null)
	{
		ArgumentException.ThrowIfNullOrEmpty(severity);
		ArgumentException.ThrowIfNullOrEmpty(code);

		using (_sync.EnterScope())
		{
			var entry = new UiDiagnosticEntry(
				++_sequence,
				DateTime.UtcNow,
				severity,
				code,
				message ?? string.Empty,
				relatedNode,
				relatedAction);

			_entries.Enqueue(entry);

			while (_entries.Count > _capacity)
				_oldestKept = _entries.Dequeue().Sequence + 1;
		}
	}

	/// <summary>
	/// Reads what was noted.
	/// </summary>
	/// <param name="query">From where, how many, and about what.</param>
	/// <returns>One page of entries.</returns>
	public UiDiagnosticPage Read(UiDiagnosticQuery query)
	{
		ArgumentNullException.ThrowIfNull(query);

		var after = 0L;

		if (!string.IsNullOrEmpty(query.AfterCursor) &&
			!long.TryParse(query.AfterCursor, NumberStyles.Integer, CultureInfo.InvariantCulture, out after))
		{
			throw UiErrors.Invalid($"'{query.AfterCursor}' is not a diagnostic cursor.");
		}

		var limit = query.Limit <= 0 ? 100 : query.Limit;

		using (_sync.EnterScope())
		{
			var matching = _entries
				.Where(entry => entry.Sequence > after)
				.Where(entry => query.RelatedNode is null || query.RelatedNode.Equals(entry.RelatedNode))
				.Where(entry => query.RelatedAction is null || query.RelatedAction == entry.RelatedAction)
				.ToArray();

			var page = matching.Take(limit).ToImmutableArray();
			var next = page.Length > 0 ? page[^1].Sequence : Math.Max(after, _sequence);

			return new UiDiagnosticPage(
				page,
				next.ToString(CultureInfo.InvariantCulture),
				after > 0 && after + 1 < _oldestKept,
				matching.Length > page.Length);
		}
	}
}
