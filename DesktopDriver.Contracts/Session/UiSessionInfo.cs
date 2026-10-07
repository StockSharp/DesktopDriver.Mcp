namespace StockSharp.DesktopDriver.Session;

using System;
using System.Collections.Immutable;

using StockSharp.DesktopDriver.Queries;

/// <summary>
/// Who answered, and what it can do.
/// </summary>
/// <param name="AppId">The product's stable identifier.</param>
/// <param name="InstanceId">This particular running copy of it.</param>
/// <param name="AppVersion">The product's version.</param>
/// <param name="ProtocolVersion">The protocol version it speaks.</param>
/// <param name="AvaloniaVersion">The UI framework version it was built against.</param>
/// <param name="FrameworkVersion">The runtime it is running on.</param>
/// <param name="FixtureId">The fixture it was started with.</param>
/// <param name="SafeTestProfile">Whether it was started with its outside world replaced.</param>
/// <param name="InputBackend">How it can send input.</param>
/// <param name="Capabilities">The operations it supports.</param>
/// <param name="Budget">The limits it applies.</param>
/// <remarks>
/// A test asserts on the product and the instance before it does anything else. Two copies of the same
/// product are running as often as not, and input sent to the wrong one is a click somebody sees.
/// </remarks>
public sealed record UiSessionInfo(
	string AppId,
	Guid InstanceId,
	string AppVersion,
	string ProtocolVersion,
	string AvaloniaVersion,
	string FrameworkVersion,
	string FixtureId,
	bool SafeTestProfile,
	string InputBackend,
	ImmutableArray<string> Capabilities,
	UiReadBudget Budget);
