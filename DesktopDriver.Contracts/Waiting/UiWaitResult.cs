namespace StockSharp.DesktopDriver.Waiting;

using System;

using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// What a satisfied wait observed.
/// </summary>
/// <param name="Elapsed">How long it took.</param>
/// <param name="ObservationCount">How many times the condition was checked.</param>
/// <param name="LastSnapshot">The node as it was when the condition held.</param>
/// <remarks>
/// Only returned when the condition actually held. A wait that ran out of time is an error carrying the
/// last thing it saw, not a result with nothing in it - the difference is a test that fails against one
/// that passes while asserting on <see langword="null"/>.
/// </remarks>
public sealed record UiWaitResult(
	TimeSpan Elapsed,
	int ObservationCount,
	UiNodeSnapshot LastSnapshot);
