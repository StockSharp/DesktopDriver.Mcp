namespace StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// A reply that says which state of the control it came from.
/// </summary>
/// <remarks>
/// An adapter reads one control and knows nothing about the running copy it belongs to or about the
/// revisions either side of the read, so it cannot stamp its own answer. The read pipeline can, and does
/// it for every reply that implements this - which is why the stamp a reply is built with is always
/// <see langword="null"/>.
/// </remarks>
public interface IUiStamped
{
	/// <summary>
	/// When it was read and at what revision.
	/// </summary>
	UiSnapshotStamp Stamp { get; }

	/// <summary>
	/// The same reply, stamped.
	/// </summary>
	/// <param name="stamp">The stamp.</param>
	/// <returns>The reply.</returns>
	IUiStamped WithStamp(UiSnapshotStamp stamp);
}
