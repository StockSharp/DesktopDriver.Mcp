namespace StockSharp.DesktopDriver.Queries;

using StockSharp.DesktopDriver.Identity;

/// <summary>
/// A bounded search for nodes.
/// </summary>
/// <param name="Selector">What to look for.</param>
/// <param name="Page">How many to return.</param>
public sealed record UiFindQuery(UiSelector Selector, UiPageRequest Page);
