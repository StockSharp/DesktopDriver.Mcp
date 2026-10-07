namespace StockSharp.DesktopDriver.States;

using System.Collections.Immutable;
using System.Text.Json.Serialization;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// One node of the docking layout.
/// </summary>
/// <param name="Id">The node's identifier in the layout.</param>
/// <param name="ParentId">The node it sits inside, when it is not the root.</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(DockGroupSnapshot), DockGroupSnapshot.KindName)]
[JsonDerivedType(typeof(DockPanelSnapshot), DockPanelSnapshot.KindName)]
public abstract record DockLayoutNode(string Id, string ParentId)
{
	/// <summary>
	/// The registered wire name of this layout node shape.
	/// </summary>
	public abstract string Kind { get; }
}

/// <summary>
/// A group of the docking layout.
/// </summary>
/// <param name="Id">The group's identifier.</param>
/// <param name="ParentId">The group it sits inside.</param>
/// <param name="GroupKind">What kind of group it is.</param>
/// <param name="Orientation">Which way it divides, when it divides.</param>
/// <param name="ChildIds">Its children, in order.</param>
/// <param name="SelectedPanelId">The selected panel, for a tab group.</param>
/// <remarks>
/// Selection belongs to the group: several tab groups can each have a selected panel at the same time,
/// and only one of them has the focus.
/// </remarks>
public sealed record DockGroupSnapshot(
	string Id,
	string ParentId,
	DockGroupKinds GroupKind,
	UiField<DockOrientations> Orientation,
	ImmutableArray<string> ChildIds,
	UiField<string> SelectedPanelId) : DockLayoutNode(Id, ParentId)
{
	/// <summary>The wire name.</summary>
	public const string KindName = "group";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>
/// A panel of the docking layout.
/// </summary>
/// <param name="Id">The panel's identifier, stable across moves.</param>
/// <param name="ParentId">The group it belongs to.</param>
/// <param name="AutomationNodeId">The panel's address in the meaning-level tree.</param>
/// <param name="Title">Its title.</param>
/// <param name="IsSelectedInGroup">Whether it is the selected tab of its group.</param>
/// <param name="HasFocus">Whether it has the focus.</param>
/// <param name="IsPinned">Whether it is pinned rather than collapsible.</param>
/// <param name="IsFloating">Whether it is in a window of its own.</param>
/// <param name="SurfaceId">The window it is drawn on.</param>
/// <param name="Presentation">How much of it the user can see.</param>
/// <param name="ContentStatus">Whether its content has been built.</param>
/// <param name="ContentRoot">The content's node, when it exists.</param>
/// <param name="BoundsInSurfaceDip">Where it is.</param>
/// <remarks>
/// The content's own state is not here. The dock adapter says which node holds the content; reading that
/// node is a separate call, answered by whichever adapter knows that control - which is why a grid never
/// has to know it is in a dock panel.
/// </remarks>
public sealed record DockPanelSnapshot(
	string Id,
	string ParentId,
	UiNodeId AutomationNodeId,
	string Title,
	bool IsSelectedInGroup,
	bool HasFocus,
	bool IsPinned,
	bool IsFloating,
	UiField<string> SurfaceId,
	DockPanelPresentations Presentation,
	UiContentStatuses ContentStatus,
	UiField<UiNodeRef> ContentRoot,
	UiField<UiRect> BoundsInSurfaceDip) : DockLayoutNode(Id, ParentId)
{
	/// <summary>The wire name.</summary>
	public const string KindName = "panel";

	/// <inheritdoc />
	public override string Kind => KindName;
}
