namespace StockSharp.DesktopDriver.Queries;

using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Which groups of a grid to read.
/// </summary>
/// <param name="ParentGroupId">The group whose children are wanted; absent means the top level.</param>
/// <param name="Page">How many to return.</param>
/// <param name="Guard">What the caller believed about the grid.</param>
public sealed record GridGroupsQuery(
	string ParentGroupId,
	UiPageRequest Page,
	UiReadGuard Guard);
