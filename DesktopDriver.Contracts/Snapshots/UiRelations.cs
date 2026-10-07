namespace StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// How one node is attached to another.
/// </summary>
public enum UiRelations
{
	/// <summary>A child in the meaning-level tree.</summary>
	Child,

	/// <summary>The content of a container, such as what a dock panel holds.</summary>
	Content,

	/// <summary>A panel belonging to a dock group.</summary>
	DockMember,

	/// <summary>A node referred to from here but owned elsewhere; not walked into by default.</summary>
	OwnedReference,
}
