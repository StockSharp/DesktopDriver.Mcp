namespace StockSharp.DesktopDriver.Host;

using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// An open endpoint and the service that answers on it.
/// </summary>
/// <param name="Service">The operations, inside the process.</param>
/// <param name="Host">The endpoint's host, which is disposed to close it.</param>
/// <param name="Endpoint">Where a runner connects.</param>
public sealed record UiAutomationEndpoint(UiAutomationService Service, UiAutomationHost Host, UiEndpointInfo Endpoint);
