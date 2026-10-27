namespace StockSharp.DesktopDriver.States;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// One block of a diagram, as the surface drew it.
/// </summary>
/// <param name="Key">The block's key in the composition, which is what a test names it by.</param>
/// <param name="TypeName">What kind of block it is.</param>
/// <param name="DisplayText">What its caption reads as.</param>
/// <param name="IsSelected">Whether it is selected.</param>
/// <param name="Sockets">The sockets it takes data through and gives data through.</param>
/// <param name="BoundsInSurfaceDip">Where it was drawn.</param>
/// <param name="Location">Where it is placed, in the diagram's own coordinates: the ones a diagramPoint names.</param>
public sealed record DiagramNodeSnapshot(
	string Key,
	UiField<string> TypeName,
	UiField<string> DisplayText,
	bool IsSelected,
	ImmutableArray<DiagramSocketSnapshot> Sockets,
	UiField<UiRect> BoundsInSurfaceDip,
	UiField<UiPoint> Location);
