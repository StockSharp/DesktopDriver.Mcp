namespace StockSharp.DesktopDriver.Adapters;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Reads one family of controls from outside.
/// </summary>
/// <remarks>
/// The preferred way to make a control readable, because it costs the control nothing: no interface, no
/// reference, no change. An adapter lives next to the library that declares the type it reads.
/// </remarks>
public interface IUiSnapshotAdapter
{
	/// <summary>
	/// The registered node kind this adapter answers as.
	/// </summary>
	string Kind { get; }

	/// <summary>
	/// Whether this adapter can read that object.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <returns><see langword="true"/> when it can.</returns>
	/// <remarks>
	/// Asked on the UI thread and expected to be cheap: no files, no network, and nothing that changes
	/// what it is being asked about.
	/// </remarks>
	bool CanHandle(UiSubject subject);

	/// <summary>
	/// What can be asked of this particular instance.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <returns>The operation names.</returns>
	/// <remarks>
	/// Of the instance, not of the interface: a grid that cannot group does not list grouping merely
	/// because the grid interface has a method for it.
	/// </remarks>
	ImmutableArray<string> GetCapabilities(UiSubject subject);

	/// <summary>
	/// Reads the control's state.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>The state, how ready it is, and what was left out.</returns>
	UiStateCapture CaptureState(UiSubject subject, UiCaptureContext context);
}
