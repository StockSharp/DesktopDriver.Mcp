namespace StockSharp.DesktopDriver.Input;

using System.Text.Json.Serialization;

/// <summary>
/// Which part of a control an action is aimed at.
/// </summary>
/// <remarks>
/// A grid is not a single target. Saying "the Price header" or "the cell of order 42" lets the adapter
/// work out where that is now, instead of the test remembering coordinates that stop being true as soon
/// as a column is resized.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(UiControlPart), UiControlPart.KindName)]
[JsonDerivedType(typeof(UiGridHeaderPart), UiGridHeaderPart.KindName)]
[JsonDerivedType(typeof(UiGridCellPart), UiGridCellPart.KindName)]
[JsonDerivedType(typeof(UiGridCellControlPart), UiGridCellControlPart.KindName)]
[JsonDerivedType(typeof(UiPropertyValuePart), UiPropertyValuePart.KindName)]
[JsonDerivedType(typeof(UiPropertyValueControlPart), UiPropertyValueControlPart.KindName)]
[JsonDerivedType(typeof(UiTreeItemPart), UiTreeItemPart.KindName)]
[JsonDerivedType(typeof(UiTreeItemControlPart), UiTreeItemControlPart.KindName)]
[JsonDerivedType(typeof(UiDockTabPart), UiDockTabPart.KindName)]
[JsonDerivedType(typeof(UiDockClosePart), UiDockClosePart.KindName)]
[JsonDerivedType(typeof(UiChartAnnotationPart), UiChartAnnotationPart.KindName)]
[JsonDerivedType(typeof(UiRibbonItemPart), UiRibbonItemPart.KindName)]
[JsonDerivedType(typeof(UiListItemPart), UiListItemPart.KindName)]
public abstract record UiTargetPart
{
	/// <summary>
	/// The registered wire name of this part shape.
	/// </summary>
	public abstract string Kind { get; }
}

/// <summary>The control itself.</summary>
public sealed record UiControlPart : UiTargetPart
{
	/// <summary>The wire name.</summary>
	public const string KindName = "control";

	/// <summary>The single instance.</summary>
	public static UiControlPart Instance { get; } = new();

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>An item of a list, by where it sits in what the list is showing.</summary>
/// <param name="Index">Its position, counted from zero.</param>
/// <remarks>
/// By position and not by a name, because a list is the one place where there is nothing else to point
/// at: its items are values rather than records, and what a person does with one is point at the first,
/// or the third. A control that sorts or filters what it shows publishes its rows as a table instead,
/// and a row of a table is addressed by a key that survives being re-sorted.
/// </remarks>
public sealed record UiListItemPart(long Index) : UiTargetPart
{
	/// <summary>The wire name.</summary>
	public const string KindName = "listItem";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>A column header of a grid.</summary>
/// <param name="ColumnId">The column.</param>
public sealed record UiGridHeaderPart(string ColumnId) : UiTargetPart
{
	/// <summary>The wire name.</summary>
	public const string KindName = "gridHeader";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>A cell of a grid.</summary>
/// <param name="RowKey">The record.</param>
/// <param name="ColumnId">The column.</param>
public sealed record UiGridCellPart(string RowKey, string ColumnId) : UiTargetPart
{
	/// <summary>The wire name.</summary>
	public const string KindName = "gridCell";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>A named control inside a cell of a grid.</summary>
/// <param name="RowKey">The record.</param>
/// <param name="ColumnId">The column.</param>
/// <param name="ControlName">The name the control has in the cell's template.</param>
/// <remarks>
/// A cell that holds an editor - a box with a picker button beside it, a "..." that opens a dialog - is one
/// cell to the grid and several things to a person. A click at the middle of the cell lands in whatever
/// covers the middle, so the button beside it is reached by its name.
/// </remarks>
public sealed record UiGridCellControlPart(string RowKey, string ColumnId, string ControlName) : UiTargetPart
{
	/// <summary>The wire name.</summary>
	public const string KindName = "gridCellControl";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>The editor of one property of a property editor.</summary>
/// <param name="Path">The property's path on the object the editor shows.</param>
/// <remarks>
/// A property editor is one control to the window and a column of fields to a person: the box that is ticked,
/// the field that is typed into. A property is named by its path, which no sorting, search or category order
/// changes.
/// </remarks>
public sealed record UiPropertyValuePart(string Path) : UiTargetPart
{
	/// <summary>The wire name.</summary>
	public const string KindName = "propertyValue";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>A control inside the editor of one property of a property editor.</summary>
/// <param name="Path">The property's path on the object the editor shows.</param>
/// <param name="ControlName">The name the control has inside the property's editor.</param>
/// <remarks>
/// The button that searches for a connection, the one that clears a password: part of one property's editor, and
/// with no address of its own, since a property editor shows its properties rather than its controls.
/// </remarks>
public sealed record UiPropertyValueControlPart(string Path, string ControlName) : UiTargetPart
{
	/// <summary>The wire name.</summary>
	public const string KindName = "propertyValueControl";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>An item of a tree.</summary>
/// <param name="ItemKey">The item's path through the tree, as the tree's items are read with it.</param>
/// <remarks>
/// A tree holds items rather than nodes. The click lands on the item's own row, not on the rows of what is
/// open inside it.
/// </remarks>
public sealed record UiTreeItemPart(string ItemKey) : UiTargetPart
{
	/// <summary>The wire name.</summary>
	public const string KindName = "treeItem";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>A control in the row of an item of a tree.</summary>
/// <param name="ItemKey">The item's path through the tree.</param>
/// <param name="ControlName">The name the control has in the item's template.</param>
/// <remarks>
/// The switch beside a source, the button beside a group: part of one item's row, and nothing of the rows
/// inside it.
/// </remarks>
public sealed record UiTreeItemControlPart(string ItemKey, string ControlName) : UiTargetPart
{
	/// <summary>The wire name.</summary>
	public const string KindName = "treeItemControl";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>An entry of a ribbon.</summary>
/// <param name="ItemId">What the entry is called.</param>
/// <remarks>
/// A ribbon's entries are not controls: they are described once and drawn wherever the ribbon decides
/// to put them, which is why one cannot be addressed as a control of its own. Naming the entry and
/// letting the ribbon say where it ended up is the same bargain a dock tab already makes.
/// </remarks>
public sealed record UiRibbonItemPart(string ItemId) : UiTargetPart
{
	/// <summary>The name this part is known by on the wire.</summary>
	public const string KindName = "ribbonItem";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>The tab of a dock panel.</summary>
/// <param name="PanelId">The panel.</param>
public sealed record UiDockTabPart(string PanelId) : UiTargetPart
{
	/// <summary>The wire name.</summary>
	public const string KindName = "dockTab";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>The close button of a dock panel.</summary>
/// <param name="PanelId">The panel.</param>
public sealed record UiDockClosePart(string PanelId) : UiTargetPart
{
	/// <summary>The wire name.</summary>
	public const string KindName = "dockClose";

	/// <inheritdoc />
	public override string Kind => KindName;
}

/// <summary>An annotation on a chart.</summary>
/// <param name="AnnotationId">The annotation.</param>
public sealed record UiChartAnnotationPart(string AnnotationId) : UiTargetPart
{
	/// <summary>The wire name.</summary>
	public const string KindName = "chartAnnotation";

	/// <inheritdoc />
	public override string Kind => KindName;
}
