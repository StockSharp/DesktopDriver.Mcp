namespace StockSharp.DesktopDriver.Cli;

/// <summary>
/// A file this program wrote out of an artifact the application holds.
/// </summary>
/// <param name="ArtifactId">Which artifact.</param>
/// <param name="Path">Where it was written, or <see langword="null"/> when it was not asked for.</param>
/// <param name="ByteLength">How big it is.</param>
public sealed record UiSavedArtifact(string ArtifactId, string Path, long ByteLength);
