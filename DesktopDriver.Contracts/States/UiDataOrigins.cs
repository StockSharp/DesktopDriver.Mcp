namespace StockSharp.DesktopDriver.States;

/// <summary>
/// Where a value in a snapshot was read from.
/// </summary>
/// <remarks>
/// A price taken from the bound object and the same price as the cell renders it are not the same fact: a
/// formatting bug lives exactly in the gap between them. The origin travels with the value so a test can
/// say which of the two it is asserting on.
/// </remarks>
public enum UiDataOrigins
{
	/// <summary>The collection the control is bound to.</summary>
	ViewModelSource,

	/// <summary>The control's own view of that collection, after sorting, filtering and grouping.</summary>
	ControlView,

	/// <summary>A visual that actually exists on screen.</summary>
	RealizedVisual,

	/// <summary>Geometry the control prepared for drawing, for controls that draw themselves.</summary>
	PreparedGeometry,
}
