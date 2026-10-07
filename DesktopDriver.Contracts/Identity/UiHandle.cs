namespace StockSharp.DesktopDriver.Identity;

using System;

/// <summary>
/// A reference to one particular live visual, valid while that visual exists.
/// </summary>
/// <param name="InstanceId">The application instance the visual lives in.</param>
/// <param name="LeaseId">The registration of this visual; a recreated visual gets a new one.</param>
/// <param name="Generation">Increases every time the same node is registered again.</param>
/// <remarks>
/// A handle is the opposite of <see cref="UiNodeId"/>: exact, and short-lived. Addressing by handle after
/// the visual was recreated is refused rather than quietly redirected - the caller asked for that visual,
/// and it is gone.
/// </remarks>
public sealed record UiHandle(Guid InstanceId, Guid LeaseId, long Generation);
