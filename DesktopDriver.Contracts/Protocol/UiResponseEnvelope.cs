namespace StockSharp.DesktopDriver.Protocol;

using System;
using System.Text.Json.Nodes;

using StockSharp.DesktopDriver.Diagnostics;

/// <summary>
/// One reply as it travels: exactly one of a result or an error.
/// </summary>
/// <param name="ProtocolVersion">The version the host speaks.</param>
/// <param name="RequestId">The request this answers.</param>
/// <param name="Result">What the operation returned.</param>
/// <param name="Error">Why it did not.</param>
public sealed record UiResponseEnvelope(
	string ProtocolVersion,
	Guid RequestId,
	JsonNode Result,
	UiError Error)
{
	/// <summary>
	/// Whether the reply carries exactly one of the two.
	/// </summary>
	public bool IsValid => (Result is null) != (Error is null);
}
