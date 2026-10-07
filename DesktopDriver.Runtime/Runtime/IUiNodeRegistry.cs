namespace StockSharp.DesktopDriver.Runtime;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Identity;

/// <summary>
/// Which live object each node address currently means.
/// </summary>
public interface IUiNodeRegistry
{
	/// <summary>
	/// Registers a node, or re-registers the same one.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="kind">The node kind.</param>
	/// <param name="visualCreated">Whether a visual exists behind it.</param>
	/// <returns>The reference callers will see.</returns>
	UiNodeRef Register(UiSubject subject, string kind, bool visualCreated);

	/// <summary>
	/// Finds what a request is about.
	/// </summary>
	/// <param name="target">The address or the handle.</param>
	/// <returns>The node and its instance.</returns>
	UiSubject Resolve(UiTarget target);

	/// <summary>
	/// Finds what an address means among the nodes already known, without searching the interface for it.
	/// </summary>
	/// <param name="id">The address.</param>
	/// <param name="subject">What it means.</param>
	/// <returns><see langword="true"/> when the address is one of the nodes already known.</returns>
	/// <remarks>
	/// This is the form the search itself uses, so that looking for a node cannot start another search
	/// for the same node.
	/// </remarks>
	bool TryResolve(UiNodeId id, out UiSubject subject);

	/// <summary>
	/// The current reference for an address.
	/// </summary>
	/// <param name="id">The address.</param>
	/// <returns>The reference, or <see langword="null"/> when nothing is registered under it.</returns>
	UiNodeRef GetReference(UiNodeId id);

	/// <summary>
	/// Forgets a node.
	/// </summary>
	/// <param name="id">The address.</param>
	void Unregister(UiNodeId id);
}
