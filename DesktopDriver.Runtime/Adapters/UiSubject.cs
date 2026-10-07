namespace StockSharp.DesktopDriver.Adapters;

using StockSharp.DesktopDriver.Identity;

/// <summary>
/// A registered node and the live object behind it.
/// </summary>
/// <param name="Id">The node's address.</param>
/// <param name="Instance">The control, or the model of a panel whose control does not exist yet.</param>
/// <remarks>
/// This never leaves the process. An <see cref="object"/> here is a local reference an adapter knows how
/// to read; nothing of the sort appears in the protocol, where every shape is declared and registered.
/// </remarks>
public sealed record UiSubject(UiNodeId Id, object Instance);
