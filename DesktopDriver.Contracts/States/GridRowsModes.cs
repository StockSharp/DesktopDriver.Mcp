namespace StockSharp.DesktopDriver.States;

/// <summary>
/// Which rows a grid read is about.
/// </summary>
public enum GridRowsModes
{
	/// <summary>
	/// The records the grid currently has, in the order it shows them, including those inside collapsed groups.
	/// </summary>
	ViewData,

	/// <summary>
	/// Only the data rows inside the visible area. Rows realised ahead of scrolling are not visible rows.
	/// </summary>
	Viewport,
}
