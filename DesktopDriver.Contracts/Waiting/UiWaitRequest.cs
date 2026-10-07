namespace StockSharp.DesktopDriver.Waiting;

using System;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;

/// <summary>
/// Wait until a node satisfies a condition.
/// </summary>
/// <param name="Target">The node.</param>
/// <param name="Condition">What to wait for.</param>
/// <param name="Timeout">How long to wait.</param>
/// <param name="CaptureOptions">How much of the node to read while checking.</param>
public sealed record UiWaitRequest(
	UiTarget Target,
	UiCondition Condition,
	TimeSpan Timeout,
	UiCaptureOptions CaptureOptions);
