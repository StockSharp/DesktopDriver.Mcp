namespace StockSharp.DesktopDriver.Waiting;

/// <summary>
/// Which of a node's revisions a condition is about.
/// </summary>
public enum UiRevisionKinds
{
	/// <summary>What the node holds.</summary>
	State,

	/// <summary>What it shows and in what order.</summary>
	View,

	/// <summary>Where it is and how big.</summary>
	Layout,
}
