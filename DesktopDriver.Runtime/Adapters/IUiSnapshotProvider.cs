namespace StockSharp.DesktopDriver.Adapters;

using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Implemented by a control that would rather answer for itself.
/// </summary>
/// <remarks>
/// Not something every control implements, and not the preferred route: an external adapter needs no
/// change to the control at all. This exists for the case where the control already has the answer
/// prepared and reading it from outside would mean rebuilding it less reliably.
/// <para>
/// Called on the UI thread, and expected to return: no awaiting, no input or output, no navigation, and
/// above all no creating of what is missing. A provider that quietly builds the content it was asked
/// about has changed the thing the caller was measuring.
/// </para>
/// </remarks>
public interface IUiSnapshotProvider
{
	/// <summary>
	/// The registered node kind this control answers as.
	/// </summary>
	string Kind { get; }

	/// <summary>
	/// Reads the control's own state.
	/// </summary>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>The state, how ready it is, and what was left out.</returns>
	UiStateCapture CaptureState(UiCaptureContext context);
}
