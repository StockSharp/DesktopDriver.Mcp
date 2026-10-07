namespace StockSharp.DesktopDriver.Input;

using System;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// What became of an action.
/// </summary>
/// <param name="ActionId">The caller's identifier.</param>
/// <param name="Status">How far it got.</param>
/// <param name="Backend">Which input backend sent it.</param>
/// <param name="Before">The node's revisions before.</param>
/// <param name="After">The node's revisions after.</param>
/// <param name="AnyInputDispatched">Whether anything at all reached the input system.</param>
/// <param name="Error">What went wrong, when something did.</param>
/// <remarks>
/// The two revisions may be equal even on success: a handler that runs later has not run yet when the
/// receipt is written. What the action achieved is established by waiting for it, not by reading this.
/// </remarks>
public sealed record UiActionReceipt(
	Guid ActionId,
	UiActionStatuses Status,
	string Backend,
	UiField<UiRevisions> Before,
	UiField<UiRevisions> After,
	bool AnyInputDispatched,
	UiError Error);
