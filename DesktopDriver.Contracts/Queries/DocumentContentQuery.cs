namespace StockSharp.DesktopDriver.Queries;

using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Which part of a document to read.
/// </summary>
/// <param name="FromLine">Start at this line, counting from one; the beginning when not given.</param>
/// <param name="Page">How many lines, and where to carry on from.</param>
/// <param name="Guard">What the caller believes it is reading.</param>
public sealed record DocumentContentQuery(
	long? FromLine,
	UiPageRequest Page,
	UiReadGuard Guard);
