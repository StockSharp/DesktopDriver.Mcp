namespace StockSharp.DesktopDriver.Diagnostics;

/// <summary>
/// Part of an artifact.
/// </summary>
/// <param name="ArtifactId">Which artifact.</param>
/// <param name="Offset">Where this part starts.</param>
/// <param name="Data">The bytes.</param>
/// <param name="IsLast">Whether the artifact ends here.</param>
/// <param name="TotalBytes">How large the whole artifact is.</param>
public sealed record UiArtifactChunk(
	string ArtifactId,
	long Offset,
	byte[] Data,
	bool IsLast,
	long TotalBytes);
