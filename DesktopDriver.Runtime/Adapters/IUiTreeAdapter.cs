namespace StockSharp.DesktopDriver.Adapters;

using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.States;

/// <summary>
/// Reads the items a tree is showing.
/// </summary>
/// <remarks>
/// A tree's shape is the thing being read: what is inside what, what is open and what is selected. A
/// caller that walked the visual children instead would get the containers of the items that happen to
/// be realised, which is a different question with a similar-looking answer.
/// </remarks>
public interface IUiTreeAdapter : IUiSnapshotAdapter
{
	/// <summary>
	/// Reads the items, in the order the tree is showing them.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="query">Which items.</param>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>One page of items.</returns>
	UiDataPage<TreeItemSnapshot> ReadItems(
		UiSubject subject,
		TreeItemsQuery query,
		UiCaptureContext context);
}
