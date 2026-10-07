namespace StockSharp.DesktopDriver.States;

using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// One block of a diagram, as the surface drew it.
/// </summary>
/// <param name="Key">The block's key in the composition, which is what a test names it by.</param>
/// <param name="TypeName">What kind of block it is.</param>
/// <param name="DisplayText">What its caption reads as.</param>
/// <param name="IsSelected">Whether it is selected.</param>
/// <param name="InputSocketCount">How many sockets it takes from.</param>
/// <param name="OutputSocketCount">How many it gives to.</param>
/// <param name="BoundsInSurfaceDip">Where it was drawn.</param>
public sealed record DiagramNodeSnapshot(
	string Key,
	UiField<string> TypeName,
	UiField<string> DisplayText,
	bool IsSelected,
	UiField<long> InputSocketCount,
	UiField<long> OutputSocketCount,
	UiField<UiRect> BoundsInSurfaceDip);
