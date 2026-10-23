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

	/// <summary>
	/// Every local file system path drawn inside the node, from where the path begins to the end of its line.
	/// </summary>
	/// <remarks>
	/// A drive, a share or a home folder of the machine the picture is taken on. Covered to the end of the line,
	/// because a path may hold spaces and where it ends cannot be told from the text.
	/// </remarks>
	Paths,
}
