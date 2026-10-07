namespace StockSharp.DesktopDriver.Adapters;

using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.States;

/// <summary>
/// Reads the blocks of a diagram and what joins them.
/// </summary>
/// <remarks>
/// Two questions rather than one, because they are asked separately: what is on the surface, and what is
/// joined to what. A caller that had to work the second out of the first would do it differently in
/// every test.
/// </remarks>
public interface IUiDiagramAdapter : IUiSnapshotAdapter
{
	/// <summary>
	/// Reads the blocks the surface drew.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="query">Which blocks.</param>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>One page of blocks.</returns>
	UiDataPage<DiagramNodeSnapshot> ReadNodes(
		UiSubject subject,
		DiagramNodesQuery query,
		UiCaptureContext context);

	/// <summary>
	/// Reads the connections between them.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="query">Which connections.</param>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>One page of connections.</returns>
	UiDataPage<DiagramConnectionSnapshot> ReadConnections(
		UiSubject subject,
		DiagramConnectionsQuery query,
		UiCaptureContext context);
}
