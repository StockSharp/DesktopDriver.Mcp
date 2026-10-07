namespace StockSharp.DesktopDriver.Adapters;

using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.States;

/// <summary>
/// Reads the text of a document.
/// </summary>
/// <remarks>
/// A line at a time, numbered the way the editor's own margin numbers them, so that a test about line
/// twelve and an editor showing line twelve mean the same line.
/// </remarks>
public interface IUiDocumentAdapter : IUiSnapshotAdapter
{
	/// <summary>
	/// Reads the lines.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="query">Which lines.</param>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>One page of lines.</returns>
	UiDataPage<DocumentLineSnapshot> ReadContent(
		UiSubject subject,
		DocumentContentQuery query,
		UiCaptureContext context);
}
