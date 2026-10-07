namespace StockSharp.DesktopDriver.Session;

using System;

/// <summary>
/// How a caller connects to a running application.
/// </summary>
/// <param name="Endpoint">Where it is listening.</param>
/// <param name="ExpectedAppId">Which product the caller means.</param>
/// <param name="ExpectedInstanceId">Which running copy of it.</param>
/// <param name="ConnectTimeout">How long to wait for the pipe.</param>
/// <remarks>
/// The expected product and instance are checked by the host, not assumed by the caller: two copies of
/// the same application are running as often as not, and input sent to the wrong one is a click
/// somebody sees.
/// </remarks>
public sealed record UiConnectOptions(
	UiEndpointInfo Endpoint,
	string ExpectedAppId,
	Guid ExpectedInstanceId,
	TimeSpan ConnectTimeout);
