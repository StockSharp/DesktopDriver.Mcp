namespace StockSharp.DesktopDriver.Runtime;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Waiting;

/// <summary>
/// Where a layer that watches real controls reports that one of them moved.
/// </summary>
/// <remarks>
/// Separate from reading them. Anything may read a revision; only the layer that actually watches the
/// controls may move one, and there is exactly one such layer per framework.
/// <para>
/// Without something on this end the whole mechanism is decorative: guards always hold because nothing
/// ever changes, and a wait for a new revision waits for ever.
/// </para>
/// </remarks>
public interface IUiRevisionSink
{
	/// <summary>
	/// Records that something about a node changed.
	/// </summary>
	/// <param name="id">The node.</param>
	/// <param name="kind">Which of its revisions moved.</param>
	void Bump(UiNodeId id, UiRevisionKinds kind);
}
