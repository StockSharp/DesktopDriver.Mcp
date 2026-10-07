namespace StockSharp.DesktopDriver.Runtime;

using System;
using System.Collections.Generic;
using System.Threading;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Waiting;

/// <summary>
/// Counts what has changed about each node.
/// </summary>
/// <remarks>
/// Reading never moves a counter. If it did, two reads in a row would look like a change, and the one
/// thing a version is for - telling "nothing happened" from "something did" - would be gone.
/// </remarks>
public sealed class UiRevisionTracker : IUiRevisionTracker, IUiRevisionSink
{
	private sealed class Entry
	{
		public Guid Epoch = Guid.NewGuid();
		public long State;
		public long View;
		public long Layout;
		public readonly List<Action<UiRevisions>> Observers = [];
	}

	private readonly Lock _sync = new();
	private readonly Dictionary<UiNodeId, Entry> _entries = [];

	/// <inheritdoc />
	public UiRevisions Read(UiNodeId id)
	{
		ArgumentNullException.ThrowIfNull(id);

		using (_sync.EnterScope())
			return ToRevisions(GetOrAdd(id));
	}

	/// <inheritdoc />
	public IDisposable Subscribe(UiNodeId id, Action<UiRevisions> observer)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(observer);

		using (_sync.EnterScope())
		{
			var entry = GetOrAdd(id);
			entry.Observers.Add(observer);

			return new Subscription(this, id, observer);
		}
	}

	/// <summary>
	/// Records that something about a node has changed.
	/// </summary>
	/// <param name="id">The node.</param>
	/// <param name="kind">Which of its versions moved.</param>
	/// <remarks>
	/// Called by whatever watches the real control - a collection changing, a sort being applied, a panel
	/// being re-docked - never by a read.
	/// </remarks>
	public void Bump(UiNodeId id, UiRevisionKinds kind)
	{
		ArgumentNullException.ThrowIfNull(id);

		Action<UiRevisions>[] observers;
		UiRevisions revisions;

		using (_sync.EnterScope())
		{
			var entry = GetOrAdd(id);

			switch (kind)
			{
				case UiRevisionKinds.State:
					entry.State++;
					break;
				case UiRevisionKinds.View:
					entry.View++;
					break;
				case UiRevisionKinds.Layout:
					entry.Layout++;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
			}

			revisions = ToRevisions(entry);
			observers = [.. entry.Observers];
		}

		foreach (var observer in observers)
			observer(revisions);
	}

	/// <summary>
	/// Records that a node's source has been replaced rather than changed.
	/// </summary>
	/// <param name="id">The node.</param>
	/// <remarks>
	/// A grid rebound to a different collection starts counting again. Saying so is what stops a caller
	/// comparing the new count with the old one and concluding the data went backwards.
	/// </remarks>
	public void ResetEpoch(UiNodeId id)
	{
		ArgumentNullException.ThrowIfNull(id);

		Action<UiRevisions>[] observers;
		UiRevisions revisions;

		using (_sync.EnterScope())
		{
			var entry = GetOrAdd(id);

			entry.Epoch = Guid.NewGuid();
			entry.State = 0;
			entry.View = 0;
			entry.Layout = 0;

			revisions = ToRevisions(entry);
			observers = [.. entry.Observers];
		}

		foreach (var observer in observers)
			observer(revisions);
	}

	/// <summary>
	/// Forgets a node.
	/// </summary>
	/// <param name="id">The node.</param>
	public void Forget(UiNodeId id)
	{
		ArgumentNullException.ThrowIfNull(id);

		using (_sync.EnterScope())
			_entries.Remove(id);
	}

	private Entry GetOrAdd(UiNodeId id)
	{
		if (!_entries.TryGetValue(id, out var entry))
		{
			entry = new Entry();
			_entries.Add(id, entry);
		}

		return entry;
	}

	private static UiRevisions ToRevisions(Entry entry)
		=> new(
			new UiRevision(entry.Epoch, entry.State),
			new UiRevision(entry.Epoch, entry.View),
			new UiRevision(entry.Epoch, entry.Layout));

	private sealed class Subscription(UiRevisionTracker tracker, UiNodeId id, Action<UiRevisions> observer) : IDisposable
	{
		private UiRevisionTracker _tracker = tracker;

		public void Dispose()
		{
			var owner = Interlocked.Exchange(ref _tracker, null);

			if (owner is null)
				return;

			using (owner._sync.EnterScope())
			{
				if (owner._entries.TryGetValue(id, out var entry))
					entry.Observers.Remove(observer);
			}
		}
	}
}
