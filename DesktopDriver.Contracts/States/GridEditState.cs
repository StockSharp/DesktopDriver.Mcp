namespace StockSharp.DesktopDriver.States;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// Whether a grid is being edited, and what the editor holds.
/// </summary>
/// <param name="IsEditing">Whether an editor is open.</param>
/// <param name="Cell">Which cell it is open on.</param>
/// <param name="EditorText">What has been typed into it.</param>
/// <param name="ValidationErrors">What it is complaining about.</param>
/// <remarks>
/// The text in an open editor is not the cell's value: that is the point of an editor. A test that
/// confuses them passes before the edit is committed.
/// </remarks>
public sealed record GridEditState(
	bool IsEditing,
	UiField<GridCellRef> Cell,
	UiField<string> EditorText,
	ImmutableArray<string> ValidationErrors);
