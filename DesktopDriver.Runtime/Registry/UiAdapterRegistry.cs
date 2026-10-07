namespace StockSharp.DesktopDriver.Runtime;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using StockSharp.DesktopDriver.Adapters;

/// <summary>
/// Which adapter reads which type.
/// </summary>
/// <remarks>
/// Resolution is by priority, then by how specifically the registration names the type: the exact type
/// beats its base, which beats an interface. Two unrelated interfaces that both match are refused
/// instead of ordered, because any order would be the order they happened to be registered in, and the
/// answer a test gets would change with a line of composition code.
/// </remarks>
public sealed class UiAdapterRegistry : IUiAdapterRegistry
{
	private sealed class Registration
	{
		public string AdapterId;
		public Type TargetType;
		public IUiSnapshotAdapter Adapter;
		public int Priority;
		public int Owners;
	}

	private sealed class Lease(UiAdapterRegistry registry, Registration registration) : IDisposable
	{
		private UiAdapterRegistry _registry = registry;

		public void Dispose()
			=> Interlocked.Exchange(ref _registry, null)?.Release(registration);
	}

	private readonly Lock _sync = new();
	private readonly List<Registration> _registrations = [];

	/// <inheritdoc />
	public IDisposable Register(string adapterId, Type targetType, IUiSnapshotAdapter adapter, int priority = 0)
	{
		ArgumentException.ThrowIfNullOrEmpty(adapterId);
		ArgumentNullException.ThrowIfNull(targetType);
		ArgumentNullException.ThrowIfNull(adapter);

		using (_sync.EnterScope())
		{
			var existing = _registrations.FirstOrDefault(item => item.AdapterId == adapterId);

			if (existing is not null)
			{
				if (existing.TargetType != targetType ||
					!ReferenceEquals(existing.Adapter, adapter) ||
					existing.Priority != priority)
				{
					throw new InvalidOperationException(
						$"Adapter '{adapterId}' is already registered for {existing.TargetType.Name}.");
				}

				existing.Owners++;

				return new Lease(this, existing);
			}

			var registration = new Registration
			{
				AdapterId = adapterId,
				TargetType = targetType,
				Adapter = adapter,
				Priority = priority,
				Owners = 1,
			};

			_registrations.Add(registration);

			return new Lease(this, registration);
		}
	}

	/// <inheritdoc />
	public IUiSnapshotAdapter Resolve(UiSubject subject)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var instanceType = subject.Instance.GetType();
		List<Registration> candidates;

		using (_sync.EnterScope())
		{
			candidates = [.. _registrations.Where(item =>
				item.TargetType.IsInstanceOfType(subject.Instance) && item.Adapter.CanHandle(subject))];
		}

		if (candidates.Count == 0)
			return null;

		var best = candidates.Max(item => item.Priority);
		var top = candidates.Where(item => item.Priority == best).ToArray();

		if (top.Length == 1)
			return top[0].Adapter;

		var classes = top
			.Where(item => !item.TargetType.IsInterface)
			.OrderBy(item => Distance(instanceType, item.TargetType))
			.ToArray();

		if (classes.Length > 0)
		{
			var closest = Distance(instanceType, classes[0].TargetType);

			if (classes.Length == 1 || Distance(instanceType, classes[1].TargetType) > closest)
				return classes[0].Adapter;

			throw UiErrors.Ambiguous(
				$"{classes[0].AdapterId} and {classes[1].AdapterId} both claim {instanceType.Name} equally closely.");
		}

		// Interfaces cannot be ranked by distance, but one of them may extend the others.
		var mostDerived = top
			.Where(item => top.All(other =>
				ReferenceEquals(other, item) || other.TargetType.IsAssignableFrom(item.TargetType)))
			.ToArray();

		if (mostDerived.Length == 1)
			return mostDerived[0].Adapter;

		throw UiErrors.Ambiguous(
			$"{top[0].AdapterId} and {top[1].AdapterId} claim {instanceType.Name} through unrelated interfaces.");
	}

	private void Release(Registration registration)
	{
		using (_sync.EnterScope())
		{
			if (--registration.Owners <= 0)
				_registrations.Remove(registration);
		}
	}

	private static int Distance(Type instanceType, Type targetType)
	{
		if (targetType == instanceType)
			return 0;

		var distance = 0;

		for (var current = instanceType.BaseType; current is not null; current = current.BaseType)
		{
			distance++;

			if (current == targetType)
				return distance;
		}

		return int.MaxValue;
	}
}
