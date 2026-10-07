namespace StockSharp.DesktopDriver.Diagnostics;

/// <summary>
/// Read part of an artifact.
/// </summary>
/// <param name="ArtifactId">Which artifact.</param>
/// <param name="Offset">Where to start.</param>
/// <param name="MaxBytes">How much to read.</param>
/// <remarks>
/// The identifier is opaque and belongs to the session's own registry. It is not a file path, and no
/// request can turn it into one: the host reads what it put there, and nothing else on the disk.
/// </remarks>
public sealed record UiArtifactReadRequest(string ArtifactId, long Offset, int MaxBytes);
