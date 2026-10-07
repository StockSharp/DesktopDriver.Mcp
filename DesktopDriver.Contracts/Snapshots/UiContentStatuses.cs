namespace StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Whether the visual behind a node exists.
/// </summary>
/// <remarks>
/// A panel that has never been opened, a panel showing an empty grid and a panel whose content was thrown
/// away when it was hidden are three different states. Reading one of them must never turn it into
/// another: a read that creates the content it was asked about has changed the thing it measured.
/// </remarks>
public enum UiContentStatuses
{
	/// <summary>Nothing has been built yet; the node is known from the model only.</summary>
	NotCreated,

	/// <summary>The visual exists.</summary>
	Created,

	/// <summary>The visual existed and has been torn down.</summary>
	Disposed,
}
