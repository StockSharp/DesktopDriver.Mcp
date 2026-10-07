namespace StockSharp.DesktopDriver.Runtime;

using System;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// What each node's versions are, and when they move.
/// </summary>
public interface IUiRevisionTracker
{
	/// <summary>
	/// The node's current versions.
	/// </summary>
	/// <param name="id">The node.</param>
	/// <returns>Its versions.</returns>
	UiRevisions Read(UiNodeId id);

	/// <summary>
	/// Watches a node's versions.
	/// </summary>
	/// <param name="id">The node.</param>
	/// <param name="observer">Called when they move.</param>
	/// <returns>A subscription.</returns>
	IDisposable Subscribe(UiNodeId id, Action<UiRevisions> observer);
}
