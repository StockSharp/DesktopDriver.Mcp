namespace StockSharp.DesktopDriver.Runner;

using System;

/// <summary>
/// The application could not be reached at all.
/// </summary>
/// <param name="message">Why not.</param>
/// <remarks>
/// Kept apart from an application that answered and said no. A caller retries the first and gives up on
/// the second, and nothing in the message tells the two apart reliably.
/// </remarks>
public sealed class UiUnreachableException(string message) : Exception(message);
