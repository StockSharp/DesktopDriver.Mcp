namespace StockSharp.DesktopDriver.States;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// What an ordinary control holds: a button, a text box, a check box, a list, a tab strip.
/// </summary>
/// <param name="Text">The text it shows.</param>
/// <param name="Value">Its value, typed.</param>
/// <param name="IsSelected">Whether it is selected within its parent.</param>
/// <param name="IsChecked">Its checked state; a known <see langword="null"/> is the indeterminate one.</param>
/// <param name="IsReadOnly">Whether it refuses editing.</param>
/// <param name="IsExpanded">Whether it is expanded.</param>
/// <param name="SelectedItem">What is selected inside it; a known <see langword="null"/> means nothing is.</param>
/// <param name="ActiveTab">Which tab is active; a known <see langword="null"/> means none is.</param>
/// <param name="ValidationErrors">What it is complaining about.</param>
/// <remarks>
/// A control that has no notion of one of these answers with an unavailable field. That is the difference
/// between "this list has nothing selected" and "this button has no selection at all", which a single
/// null could not tell apart.
/// </remarks>
public sealed record BasicState(
	UiField<string> Text,
	UiField<UiValue> Value,
	UiField<bool> IsSelected,
	UiField<bool?> IsChecked,
	UiField<bool> IsReadOnly,
	UiField<bool> IsExpanded,
	UiField<UiNodeId> SelectedItem,
	UiField<UiNodeId> ActiveTab,
	ImmutableArray<string> ValidationErrors) : UiState
{
	/// <inheritdoc />
	public override string Kind => UiStateKinds.Basic;
}
