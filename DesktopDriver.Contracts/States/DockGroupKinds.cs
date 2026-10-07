namespace StockSharp.DesktopDriver.States;

/// <summary>
/// What a node of the docking layout is.
/// </summary>
public enum DockGroupKinds
{
	/// <summary>The top of the layout.</summary>
	Root,

	/// <summary>A splitter dividing its children.</summary>
	Split,

	/// <summary>A set of panels sharing one place, one of them selected.</summary>
	Tabs,

	/// <summary>A set of panels collapsed to an edge.</summary>
	AutoHide,
}
