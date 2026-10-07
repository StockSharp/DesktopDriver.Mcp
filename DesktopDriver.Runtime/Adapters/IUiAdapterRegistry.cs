namespace StockSharp.DesktopDriver.Adapters;

using System;

/// <summary>
/// Which adapter reads which type.
/// </summary>
public interface IUiAdapterRegistry
{
	/// <summary>
	/// Registers an adapter for a type.
	/// </summary>
	/// <param name="adapterId">The registration's identity.</param>
	/// <param name="targetType">The type it reads.</param>
	/// <param name="adapter">The adapter.</param>
	/// <param name="priority">Higher wins over lower.</param>
	/// <returns>A lease that removes the registration when it is disposed.</returns>
	/// <remarks>
	/// Registering the same adapter for the same type twice is allowed and shares one registration: two
	/// modules may legitimately both depend on a third. Disposing one of their leases must not unregister
	/// the other's, so ownership is counted.
	/// </remarks>
	IDisposable Register(string adapterId, Type targetType, IUiSnapshotAdapter adapter, int priority = 0);

	/// <summary>
	/// Finds the adapter for an object.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <returns>The adapter, or <see langword="null"/> when nothing claims it.</returns>
	/// <remarks>
	/// Higher priority first, then the exact type, then the nearest base type. Two unrelated interface
	/// registrations that both match are a conflict rather than a coin toss: whichever won would be an
	/// accident of registration order, and the reply would change with it.
	/// </remarks>
	IUiSnapshotAdapter Resolve(UiSubject subject);
}
