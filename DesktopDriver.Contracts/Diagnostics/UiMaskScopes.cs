namespace StockSharp.DesktopDriver.Diagnostics;

/// <summary>
/// What a mask covers.
/// </summary>
public enum UiMaskScopes
{
	/// <summary>
	/// The node, or the part of it the mask names.
	/// </summary>
	Node,

	/// <summary>
	/// Every place in the picture where the text the node shows is drawn, the node itself included.
	/// </summary>
	Text,
}
