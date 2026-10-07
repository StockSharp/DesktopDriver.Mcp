namespace StockSharp.DesktopDriver.Snapshots;

using StockSharp.DesktopDriver.Identity;

/// <summary>
/// One edge of the meaning-level tree.
/// </summary>
/// <param name="ParentId">The node the edge starts at.</param>
/// <param name="Child">The node it leads to.</param>
/// <param name="Relation">What kind of attachment it is.</param>
/// <param name="Order">The position among the parent's edges of the same kind.</param>
/// <remarks>
/// The tree travels as nodes plus edges rather than as nesting, so a node that appears in two places -
/// a document shown in a tab and referred to from a list - is sent once and pointed at twice.
/// </remarks>
public sealed record UiChildLink(
	UiNodeId ParentId,
	UiNodeRef Child,
	UiRelations Relation,
	int Order);
