namespace StockSharp.DesktopDriver.Runtime;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Input;

/// <summary>
/// Works out where an action will land.
/// </summary>
public interface IUiInputTargetResolver
{
	/// <summary>
	/// Finds where a part of a control is.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="part">The part.</param>
	/// <param name="action">What will be done to it.</param>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>Where the action will land.</returns>
	UiResolvedInputTarget Resolve(
		UiSubject subject,
		UiTargetPart part,
		UiInputAction action,
		UiCaptureContext context);
}
