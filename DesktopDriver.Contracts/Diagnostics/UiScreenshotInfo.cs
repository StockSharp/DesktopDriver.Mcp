namespace StockSharp.DesktopDriver.Diagnostics;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// The picture that was taken, as a reference rather than as bytes.
/// </summary>
/// <param name="ArtifactId">Where the bytes are kept.</param>
/// <param name="MimeType">What kind of image it is.</param>
/// <param name="PixelWidth">Its width in pixels.</param>
/// <param name="PixelHeight">Its height in pixels.</param>
/// <param name="ByteLength">How large it is.</param>
/// <param name="Stamp">When it was taken and at what revision.</param>
/// <param name="CaptureKind">How it was taken.</param>
/// <param name="RenderScaling">The scaling of the display it came from.</param>
/// <param name="Exclusions">What was deliberately left out of it.</param>
/// <remarks>
/// A reference, not the bytes: an image inlined into every reply would dwarf the data it illustrates, and
/// most replies are read by something that pays per token.
/// </remarks>
public sealed record UiScreenshotInfo(
	string ArtifactId,
	string MimeType,
	int PixelWidth,
	int PixelHeight,
	long ByteLength,
	UiSnapshotStamp Stamp,
	string CaptureKind,
	UiField<double> RenderScaling,
	ImmutableArray<string> Exclusions);
