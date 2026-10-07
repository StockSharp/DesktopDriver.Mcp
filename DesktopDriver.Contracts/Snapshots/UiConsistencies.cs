namespace StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// How much the reader can rely on a snapshot having been taken all at once.
/// </summary>
/// <remarks>
/// A snapshot is a copy of a moving thing. Saying how it was taken is cheaper than pretending every read
/// is atomic, and it is what lets a test tell a real change from a torn read.
/// </remarks>
public enum UiConsistencies
{
	/// <summary>Copied in one pass on the UI thread.</summary>
	UiThreadRead,

	/// <summary>Copied and then confirmed unchanged by comparing revisions.</summary>
	VersionChecked,

	/// <summary>Assembled from parts that could have moved between them.</summary>
	BestEffort,
}
