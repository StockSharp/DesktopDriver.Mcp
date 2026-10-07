namespace StockSharp.DesktopDriver.States;

/// <summary>
/// How much of a panel the user can currently see.
/// </summary>
/// <remarks>
/// A panel that exists, a panel whose tab exists but is not selected, a panel collapsed to an edge and a
/// panel that was closed are four states. Collapsing them into one boolean is how a test comes to believe
/// that an unselected tab is a missing panel.
/// </remarks>
public enum DockPanelPresentations
{
	/// <summary>On screen.</summary>
	Shown,

	/// <summary>Has a tab in a group, but another tab is selected.</summary>
	HiddenTab,

	/// <summary>Collapsed to an edge.</summary>
	AutoHidden,

	/// <summary>In the layout but not shown anywhere.</summary>
	Hidden,

	/// <summary>The docking model does not say.</summary>
	Unknown,
}
