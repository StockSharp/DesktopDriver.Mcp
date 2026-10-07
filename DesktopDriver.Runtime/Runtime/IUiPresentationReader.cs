namespace StockSharp.DesktopDriver.Runtime;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Reads where a node is and whether it would take input.
/// </summary>
/// <remarks>
/// The same for every control, which is why it is not an adapter's business: geometry, visibility and
/// focus are the windowing system's answers, and an adapter that reimplemented them would be wrong in a
/// different way for each control.
/// </remarks>
public interface IUiPresentationReader
{
	/// <summary>
	/// Reads a node's presentation.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>The presentation.</returns>
	UiPresentation Read(UiSubject subject, UiCaptureContext context);
}
