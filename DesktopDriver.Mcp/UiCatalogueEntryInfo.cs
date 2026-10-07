namespace StockSharp.DesktopDriver.Mcp;

/// <summary>
/// One application this server may be asked to start.
/// </summary>
/// <param name="AppId">What to pass as <c>appId</c> to start it.</param>
/// <param name="Description">What it is.</param>
/// <param name="IsBuilt">Whether its executable is actually there.</param>
/// <param name="RunningAs">The instances of it already running, if any.</param>
public sealed record UiCatalogueEntryInfo(
	string AppId,
	string Description,
	bool IsBuilt,
	string[] RunningAs);
