namespace StockSharp.DesktopDriver.Diagnostics;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Take a picture of a node.
/// </summary>
/// <param name="Target">What to picture.</param>
/// <param name="CaptureKind">One of <see cref="UiCaptureKinds"/>.</param>
/// <param name="IncludePopups">Whether popups over it should be composed in.</param>
/// <param name="MaxPixelWidth">The widest the picture may be.</param>
/// <param name="MaxPixelHeight">The tallest the picture may be.</param>
/// <param name="Guard">What the caller believed about the node.</param>
/// <param name="Mask">What to keep out of the picture.</param>
/// <remarks>
/// The kind that was asked for is the kind that is taken, or the request fails: quietly redrawing a
/// control when the caller asked for the window would answer a question nobody asked.
/// <para>
/// What <see cref="Mask"/> covers is blurred in the frame before the frame is written anywhere, so a
/// picture of it never exists - not in a file, not in an artifact. A window that shows who is signed in
/// can be pictured without picturing them. A mask naming something that is not there fails the request
/// rather than being skipped.
/// </para>
/// </remarks>
public sealed record UiScreenshotRequest(
	UiTarget Target,
	string CaptureKind,
	bool IncludePopups,
	int MaxPixelWidth,
	int MaxPixelHeight,
	UiReadGuard Guard,
	ImmutableArray<UiMask> Mask);
