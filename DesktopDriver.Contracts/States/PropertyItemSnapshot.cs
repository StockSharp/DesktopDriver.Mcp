namespace StockSharp.DesktopDriver.States;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// One property of an object, as an editor is showing it.
/// </summary>
/// <param name="Path">The property's path on the object, which is what a test names it by.</param>
/// <param name="ParentPath">The property it sits inside, for a nested one.</param>
/// <param name="DisplayName">What the editor calls it.</param>
/// <param name="Category">The category it is grouped under.</param>
/// <param name="ValueTypeName">The type of its value.</param>
/// <param name="Value">Its value, typed.</param>
/// <param name="DisplayText">What the value reads as.</param>
/// <param name="EditorKind">What kind of editor the property calls for.</param>
/// <param name="IsReadOnly">Whether this property refuses changes.</param>
/// <param name="IsExpanded">Whether its own properties are showing.</param>
/// <param name="HasNonDefaultValue">Whether its value differs from the default it declares.</param>
/// <param name="Depth">How deep it is nested.</param>
/// <param name="ChildCount">How many properties it has of its own, once it has been opened.</param>
/// <param name="ValidationError">Why the editor is refusing the value, when it is.</param>
/// <remarks>
/// The path is the key, because it is what the property is: a position in a list changes with the
/// category order, the basic mode and the search box, and the path changes with none of them.
/// <para>
/// A property that has never been opened has no count of its own properties, because the editor has not
/// built them; the field says so rather than answering zero, which would read as "it has none".
/// </para>
/// </remarks>
public sealed record PropertyItemSnapshot(
	string Path,
	string ParentPath,
	string DisplayName,
	string Category,
	string ValueTypeName,
	UiField<UiValue> Value,
	UiField<string> DisplayText,
	string EditorKind,
	bool IsReadOnly,
	bool IsExpanded,
	bool HasNonDefaultValue,
	int Depth,
	UiField<long> ChildCount,
	UiField<string> ValidationError);
