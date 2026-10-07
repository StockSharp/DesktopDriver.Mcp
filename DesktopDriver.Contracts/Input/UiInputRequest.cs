namespace StockSharp.DesktopDriver.Input;

using System;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// One thing to do to the interface.
/// </summary>
/// <param name="ActionId">The caller's identifier for this action, made before the first attempt.</param>
/// <param name="Target">Which node.</param>
/// <param name="Part">Which part of it.</param>
/// <param name="Action">What to do.</param>
/// <param name="Guard">What the caller believed about the node.</param>
/// <param name="Timeout">How long to keep trying.</param>
/// <remarks>
/// The identifier is the caller's, and it is what makes a dropped connection safe: asking again with the
/// same identifier returns the original outcome instead of clicking a second time.
/// </remarks>
public sealed record UiInputRequest(
	Guid ActionId,
	UiTarget Target,
	UiTargetPart Part,
	UiInputAction Action,
	UiReadGuard Guard,
	TimeSpan Timeout);
