namespace StockSharp.DesktopDriver.Runtime;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Identity;

/// <summary>
/// Finds a node that nothing has read yet.
/// </summary>
/// <remarks>
/// The registry only knows the nodes something has already looked at, and a caller naming a control by
/// its identifier has usually looked at nothing. Requiring it to walk the whole interface first, purely
/// so that the module notices the control exists, would make every test start with a step that has
/// nothing to do with what it is testing.
/// </remarks>
public interface IUiNodeLocator
{
	/// <summary>
	/// Searches the interface for a node with this address.
	/// </summary>
	/// <param name="id">The address.</param>
	/// <returns>The node, or <see langword="null"/> when the interface has no such node right now.</returns>
	/// <remarks>
	/// Runs on the thread the interface belongs to, and reads only: a node that has not been created is
	/// not created by looking for it.
	/// </remarks>
	UiSubject Locate(UiNodeId id);
}
