namespace StockSharp.DesktopDriver.States;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// One connection of a diagram, as the surface drew it.
/// </summary>
/// <param name="Key">Which blocks and sockets it joins, written out.</param>
/// <param name="FromNodeKey">The block it leaves.</param>
/// <param name="FromSocketId">The socket it leaves from.</param>
/// <param name="ToNodeKey">The block it reaches.</param>
/// <param name="ToSocketId">The socket it reaches.</param>
/// <param name="RoutePointCount">How many corners the drawn line has.</param>
/// <param name="RouteInSurfaceDip">The line as it was drawn, corner by corner.</param>
/// <remarks>
/// The route is the polyline the surface actually drew around the blocks in the way, not a straight
/// line between two sockets. A connection the model holds but nothing drew has no route, and says so
/// rather than answering an empty one.
/// </remarks>
public sealed record DiagramConnectionSnapshot(
	string Key,
	string FromNodeKey,
	string FromSocketId,
	string ToNodeKey,
	string ToSocketId,
	UiField<long> RoutePointCount,
	UiField<ImmutableArray<UiPoint>> RouteInSurfaceDip);
