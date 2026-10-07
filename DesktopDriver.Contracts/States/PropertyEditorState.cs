namespace StockSharp.DesktopDriver.States;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// What a property editor is showing.
/// </summary>
/// <param name="EntityType">The type of the object being edited.</param>
/// <param name="EntityId">What that object calls itself, when it says.</param>
/// <param name="ItemCount">How many properties are on show, the nested ones included.</param>
/// <param name="IsCategorized">Whether the properties are grouped by category.</param>
/// <param name="IsBasicMode">Whether only the everyday properties are shown.</param>
/// <param name="IsReadOnly">Whether the whole editor refuses changes.</param>
/// <param name="SearchText">What the properties are being filtered by.</param>
/// <param name="HasNonDefaultValues">Whether anything holds a value other than the default it declares.</param>
/// <param name="ValidationErrors">What the editor is complaining about, one message per property.</param>
/// <remarks>
/// The properties themselves are read separately and a page at a time: an object with a hundred
/// properties, several of them expandable, is an ordinary object.
/// <para>
/// Which properties are on show is a state of the editor, not of the object: the basic mode and the
/// search box both change it, and a test that read the object instead would not see either.
/// </para>
/// <para>
/// This editor reports a value as non-default rather than as changed: what it can tell is whether a
/// property would be serialised, which is a comparison against the default it declares and not a
/// history of what the user did.
/// </para>
/// </remarks>
public sealed record PropertyEditorState(
	UiField<string> EntityType,
	UiField<string> EntityId,
	UiField<long> ItemCount,
	bool IsCategorized,
	bool IsBasicMode,
	bool IsReadOnly,
	UiField<string> SearchText,
	bool HasNonDefaultValues,
	ImmutableArray<string> ValidationErrors) : UiState
{
	/// <inheritdoc />
	public override string Kind => UiStateKinds.PropertyEditor;
}
