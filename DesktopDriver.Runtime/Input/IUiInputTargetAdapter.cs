namespace StockSharp.DesktopDriver.Runtime;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Input;

/// <summary>
/// Implemented by an adapter whose control has parts only it can find.
/// </summary>
/// <remarks>
/// A grid knows where the header of a column is and where the cell of a record is; a chart knows where
/// an annotation is. Without this the caller would be left to compute coordinates, which is the thing
/// the whole protocol is arranged to avoid.
/// </remarks>
public interface IUiInputTargetAdapter
{
	/// <summary>
	/// Whether this adapter can find that part.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="part">The part.</param>
	/// <returns><see langword="true"/> when it can.</returns>
	bool SupportsTargetPart(UiSubject subject, UiTargetPart part);

	/// <summary>
	/// Finds where a part is.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="part">The part.</param>
	/// <param name="action">What will be done to it.</param>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>Where the action will land.</returns>
	UiResolvedInputTarget ResolveInputTarget(
		UiSubject subject,
		UiTargetPart part,
		UiInputAction action,
		UiCaptureContext context);
}
