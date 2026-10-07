namespace StockSharp.DesktopDriver.Session;

using System;

/// <summary>
/// Where to reach a running instance.
/// </summary>
/// <param name="AppId">The product.</param>
/// <param name="InstanceId">The running copy.</param>
/// <param name="PipeName">The local channel it listens on.</param>
/// <param name="ProtocolVersion">The protocol version it speaks.</param>
/// <param name="ProcessId">Its process.</param>
/// <remarks>
/// Published in the endpoint file so callers can open a session over the local channel.
/// </remarks>
public sealed record UiEndpointInfo(
	string AppId,
	Guid InstanceId,
	string PipeName,
	string ProtocolVersion,
	int ProcessId);
