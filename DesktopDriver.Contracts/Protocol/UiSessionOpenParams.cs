namespace StockSharp.DesktopDriver.Protocol;

using System;

/// <summary>
/// The first message of a connection.
/// </summary>
/// <param name="ExpectedAppId">Which product the caller believes it reached.</param>
/// <param name="ExpectedInstanceId">Which running copy of it.</param>
/// <param name="ProtocolVersion">The version the caller speaks.</param>
/// <remarks>
/// Operations are accepted only after the expected product, instance and protocol have been checked.
/// </remarks>
public sealed record UiSessionOpenParams(
	string ExpectedAppId,
	Guid ExpectedInstanceId,
	string ProtocolVersion);
