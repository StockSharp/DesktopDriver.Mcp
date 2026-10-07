namespace StockSharp.DesktopDriver.Runtime;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Where an action will actually land.
/// </summary>
/// <param name="Node">The node that will receive it.</param>
/// <param name="SurfaceId">The window it is on.</param>
/// <param name="BoundsInSurfaceDip">Where that node is.</param>
/// <param name="PointInSurfaceDip">The point the pointer will be put at.</param>
/// <param name="Revisions">What the node's versions were when this was worked out.</param>
/// <remarks>
/// Never leaves the process. A test says "the Price header"; where that is on screen right now is the
/// module's business, and a test that remembered coordinates would be wrong after the first resize.
/// </remarks>
public sealed record UiResolvedInputTarget(
	UiNodeRef Node,
	string SurfaceId,
	UiRect BoundsInSurfaceDip,
	UiPoint PointInSurfaceDip,
	UiRevisions Revisions);
