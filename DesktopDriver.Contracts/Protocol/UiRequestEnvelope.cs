namespace StockSharp.DesktopDriver.Protocol;

using System;
using System.Text.Json.Nodes;

/// <summary>
/// One request as it travels.
/// </summary>
/// <param name="ProtocolVersion">The version the caller speaks.</param>
/// <param name="RequestId">This request, so its reply can be matched to it.</param>
/// <param name="InstanceId">Which running application the caller means.</param>
/// <param name="Method">One of <see cref="UiMethods"/>.</param>
/// <param name="TimeoutMs">How long the caller will wait.</param>
/// <param name="Parameters">The method's own arguments.</param>
/// <remarks>
/// The instance is named in every request, not assumed from the connection. Two copies of the same
/// product are running as often as not, and input sent to the wrong one is a click somebody sees.
/// </remarks>
public sealed record UiRequestEnvelope(
	string ProtocolVersion,
	Guid RequestId,
	Guid InstanceId,
	string Method,
	int TimeoutMs,
	JsonNode Parameters);
