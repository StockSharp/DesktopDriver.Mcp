namespace StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// How far a node is from being able to answer about itself.
/// </summary>
public enum UiReadyStatuses
{
	/// <summary>Readiness is not something this node reports.</summary>
	Unknown,

	/// <summary>Data is on its way.</summary>
	Loading,

	/// <summary>The node is showing what it is meant to show.</summary>
	Ready,

	/// <summary>The node failed to reach a usable state.</summary>
	Error,
}
