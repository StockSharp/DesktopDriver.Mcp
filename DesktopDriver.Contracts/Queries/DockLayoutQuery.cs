namespace StockSharp.DesktopDriver.Queries;

using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Which part of a docking layout to read.
/// </summary>
/// <param name="RootLayoutId">The subtree to read; absent means the whole workspace.</param>
/// <param name="Budget">How much the reply may carry.</param>
/// <param name="Guard">What the caller believed about the workspace.</param>
public sealed record DockLayoutQuery(
	string RootLayoutId,
	UiReadBudget Budget,
	UiReadGuard Guard);
