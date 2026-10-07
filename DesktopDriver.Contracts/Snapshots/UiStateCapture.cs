namespace StockSharp.DesktopDriver.Snapshots;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.States;

/// <summary>
/// What an adapter read out of one control.
/// </summary>
/// <param name="State">The control's own state.</param>
/// <param name="Ready">How ready it was.</param>
/// <param name="Completeness">What was left out.</param>
/// <param name="Warnings">What the adapter wants the reader to know.</param>
/// <remarks>
/// This is the adapter's whole answer. Identity, geometry and the envelope are added by the service, so
/// an adapter never has to know how a node is addressed or what a stamp looks like.
/// </remarks>
public sealed record UiStateCapture(
	UiState State,
	UiReadyStatuses Ready,
	UiCompleteness Completeness,
	ImmutableArray<string> Warnings);
