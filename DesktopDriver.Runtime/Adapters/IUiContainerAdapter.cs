namespace StockSharp.DesktopDriver.Adapters;

using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Reads what a container holds, one page at a time.
/// </summary>
/// <remarks>
/// Only the immediate children, and only as links. Walking further is the tree builder's job, which is
/// what keeps a container from having to know how anything inside it is read.
/// </remarks>
public interface IUiContainerAdapter
{
	/// <summary>
	/// Reads the container's immediate children.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="query">Which page.</param>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>One page of links.</returns>
	UiDataPage<UiChildLink> ReadChildren(
		UiSubject subject,
		UiPageRequest query,
		UiCaptureContext context);
}
