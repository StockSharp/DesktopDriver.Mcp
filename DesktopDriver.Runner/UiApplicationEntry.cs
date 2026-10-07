namespace StockSharp.DesktopDriver.Runner;

/// <summary>
/// One application a caller is allowed to start.
/// </summary>
/// <param name="AppId">The product's stable identifier.</param>
/// <param name="Executable">Where its instrumented build is.</param>
/// <param name="Arguments">Anything the product itself needs, separated by spaces.</param>
/// <param name="Description">What it is, for whoever is choosing.</param>
/// <remarks>
/// A caller names an application from this list and never a path of its own. A server that started
/// whatever path it was handed would be a way to run anything on the machine, dressed up as automation.
/// </remarks>
public sealed record UiApplicationEntry(
	string AppId,
	string Executable,
	string Arguments,
	string Description);
