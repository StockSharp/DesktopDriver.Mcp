namespace StockSharp.DesktopDriver.Adapters;

using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.States;

/// <summary>
/// Reads the properties a property editor is showing.
/// </summary>
/// <remarks>
/// What is on show is a state of the editor rather than of the object behind it: the basic mode, the
/// categories and the search box all change it. A test that read the object instead would see none of
/// them, and would pass with the editor showing nothing at all.
/// </remarks>
public interface IUiPropertyEditorAdapter : IUiSnapshotAdapter
{
	/// <summary>
	/// Reads the properties, in the order the editor is showing them.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="query">Which properties.</param>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>One page of properties.</returns>
	UiDataPage<PropertyItemSnapshot> ReadItems(
		UiSubject subject,
		PropertyEditorItemsQuery query,
		UiCaptureContext context);
}
