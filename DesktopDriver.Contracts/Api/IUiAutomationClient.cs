namespace StockSharp.DesktopDriver.Api;

using System;

/// <summary>
/// The same operations, reached across a channel.
/// </summary>
/// <remarks>
/// It adds nothing to the interface. A client with operations of its own would be a second
/// implementation of the protocol, and the two would answer differently the first time one changed.
/// </remarks>
public interface IUiAutomationClient : IUiAutomationApi, IAsyncDisposable;
