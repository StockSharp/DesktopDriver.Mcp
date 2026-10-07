namespace StockSharp.DesktopDriver.Adapters;

using StockSharp.DesktopDriver.Queries;

/// <summary>
/// Reads a docking layout: its groups, its panels, and which node holds each panel's content.
/// </summary>
/// <remarks>
/// It says where the content is, never what the content holds. That boundary is what lets the same grid
/// be read identically in a docked panel, a floating window and an ordinary window.
/// </remarks>
public interface IUiDockAdapter : IUiSnapshotAdapter, IUiContainerAdapter
{
	/// <summary>
	/// Reads the layout.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="query">Which part of it.</param>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>The layout.</returns>
	DockLayoutSnapshot ReadLayout(
		UiSubject subject,
		DockLayoutQuery query,
		UiCaptureContext context);
}
