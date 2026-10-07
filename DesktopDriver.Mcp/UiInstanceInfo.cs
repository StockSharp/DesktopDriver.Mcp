namespace StockSharp.DesktopDriver.Mcp;

/// <summary>
/// One application this server is talking to, as a tool reports it.
/// </summary>
/// <param name="Instance">What to pass as <c>instance</c> to every other tool.</param>
/// <param name="AppId">Which product it is.</param>
/// <param name="ProcessId">Its process.</param>
/// <param name="WasStartedHere">Whether this server started it, and may therefore close it.</param>
public sealed record UiInstanceInfo(string Instance, string AppId, int ProcessId, bool WasStartedHere);
