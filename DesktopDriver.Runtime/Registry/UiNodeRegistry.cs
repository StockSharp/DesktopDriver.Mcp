namespace StockSharp.DesktopDriver.Runtime;

using System;
using System.Collections.Generic;
using System.Threading;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Identity;

/// <summary>
/// Remembers which live object each node address currently means.
/// </summary>
/// <remarks>
/// Instances are held weakly. A registry that held them strongly would keep every window the user ever
/// closed, and the first symptom would be the application growing while a test suite runs.
/// <para>
/// Re-registering the same live instance keeps its handle, so a panel that is merely moved does not
/// invalidate references a caller already has. Registering a different instance under the same address
/// takes a new lease: the address still means the same thing, but the visual behind it is new, and a
/// caller holding the old handle asked for the old visual.
/// </para>
/// </remarks>
public sealed class UiNodeRegistry : IUiNodeRegistry
{
	private sealed class Entry
	{
		public WeakReference<object> Instance;
		public UiHandle Handle;
		public string Kind;
		public bool VisualCreated;
	}

	private readonly Lock _sync = new();
	private readonly Dictionary<UiNodeId, Entry> _byId = [];
	private readonly Dictionary<Guid, UiNodeId> _byLease = [];
	private readonly Guid _instanceId;

	/// <summary>
	/// Initializes a new instance of the <see cref="UiNodeRegistry"/> class.
	/// </summary>
	/// <param name="instanceId">The application instance these nodes belong to.</param>
	public UiNodeRegistry(Guid instanceId)
	{
		_instanceId = instanceId;
	}

	/// <inheritdoc />
	public UiNodeRef Register(UiSubject subject, string kind, bool visualCreated)
	{
		ArgumentNullException.ThrowIfNull(subject);
		ArgumentException.ThrowIfNullOrEmpty(kind);

		using (_sync.EnterScope())
		{
			if (_byId.TryGetValue(subject.Id, out var existing) &&
				existing.Instance.TryGetTarget(out var alive) &&
				ReferenceEquals(alive, subject.Instance))
			{
				existing.Kind = kind;
				existing.VisualCreated = visualCreated;

				return ToReference(subject.Id, existing);
			}

			var generation = existing is null ? 1 : existing.Handle.Generation + 1;

			if (existing is not null)
				_byLease.Remove(existing.Handle.LeaseId);

			var entry = new Entry
			{
				Instance = new WeakReference<object>(subject.Instance),
				Handle = new UiHandle(_instanceId, Guid.NewGuid(), generation),
				Kind = kind,
				VisualCreated = visualCreated,
			};

			_byId[subject.Id] = entry;
			_byLease[entry.Handle.LeaseId] = subject.Id;

			return ToReference(subject.Id, entry);
		}
	}

	/// <summary>
	/// Finds nodes nothing has read yet.
	/// </summary>
	/// <remarks>
	/// Set after construction because the two need each other: a search binds what it finds, and binding
	/// is what this registry is.
	/// </remarks>
	public IUiNodeLocator Locator { get; set; }

	/// <summary>
	/// Whether an instance still stands where a caller can reach it.
	/// </summary>
	/// <remarks>
	/// Answered by the backend, which alone knows what being on screen means for its visuals; left unset,
	/// every instance that has not been collected counts as reachable.
	/// </remarks>
	public Func<object, bool> IsLive { get; set; }

	/// <inheritdoc />
	public UiSubject Resolve(UiTarget target)
	{
		ArgumentNullException.ThrowIfNull(target);

		if (!target.IsValid)
			throw UiErrors.Invalid("A target names a node either by its address or by its handle, not both and not neither.");

		var id = target.Id;

		if (id is null)
		{
			using (_sync.EnterScope())
			{
				if (!_byLease.TryGetValue(target.Handle.LeaseId, out id))
					throw UiErrors.Stale("That visual no longer exists.");

				var byHandle = _byId[id];

				if (byHandle.Handle != target.Handle)
					throw UiErrors.Stale("That visual has been replaced since the handle was issued.");

				return ToSubject(id, byHandle);
			}
		}

		UiSubject known = null;
		var collected = false;

		using (_sync.EnterScope())
		{
			if (_byId.TryGetValue(id, out var entry))
			{
				if (entry.Instance.TryGetTarget(out var instance))
					known = new UiSubject(id, instance);
				else
					collected = true;
			}
		}

		if (known is not null && (IsLive?.Invoke(known.Instance) ?? true))
			return known;

		// Either nothing has looked at this node yet, which is the normal state of affairs for the first thing
		// a caller asks about, or what was registered under it has left the screen: a page rebuilt under the
		// same address is a new visual. Searched for outside the lock, because the search reads the interface.
		if (Locator?.Locate(id) is { } located)
			return located;

		if (known is not null)
			return known;

		throw collected
			? UiErrors.Stale($"The object behind {id} has been collected.")
			: UiErrors.NotFound($"Nothing is registered as {id}.");
	}

	/// <inheritdoc />
	public bool TryResolve(UiNodeId id, out UiSubject subject)
	{
		ArgumentNullException.ThrowIfNull(id);

		using (_sync.EnterScope())
		{
			if (!_byId.TryGetValue(id, out var entry))
			{
				subject = null;

				return false;
			}

			subject = ToSubject(id, entry);

			return true;
		}
	}

	/// <inheritdoc />
	public UiNodeRef GetReference(UiNodeId id)
	{
		ArgumentNullException.ThrowIfNull(id);

		using (_sync.EnterScope())
			return _byId.TryGetValue(id, out var entry) ? ToReference(id, entry) : null;
	}

	/// <inheritdoc />
	public void Unregister(UiNodeId id)
	{
		ArgumentNullException.ThrowIfNull(id);

		using (_sync.EnterScope())
		{
			if (!_byId.Remove(id, out var entry))
				return;

			_byLease.Remove(entry.Handle.LeaseId);
		}
	}

	/// <summary>
	/// Drops the addresses whose instances have been collected.
	/// </summary>
	/// <returns>How many were dropped.</returns>
	/// <remarks>
	/// Called when something is known to have closed. Until then a collected entry answers
	/// <see cref="StockSharp.DesktopDriver.Diagnostics.UiErrorCodes.StaleElement"/>, which is the truth
	/// about it anyway.
	/// </remarks>
	public int Prune()
	{
		using (_sync.EnterScope())
		{
			var dead = new List<UiNodeId>();

			foreach (var (id, entry) in _byId)
			{
				if (!entry.Instance.TryGetTarget(out _))
					dead.Add(id);
			}

			foreach (var id in dead)
			{
				_byLease.Remove(_byId[id].Handle.LeaseId);
				_byId.Remove(id);
			}

			return dead.Count;
		}
	}

	private static UiNodeRef ToReference(UiNodeId id, Entry entry)
		=> new(id, entry.VisualCreated ? entry.Handle : null, entry.Kind);

	private static UiSubject ToSubject(UiNodeId id, Entry entry)
	{
		if (!entry.Instance.TryGetTarget(out var instance))
			throw UiErrors.Stale($"The object behind {id} has been collected.");

		return new UiSubject(id, instance);
	}
}
