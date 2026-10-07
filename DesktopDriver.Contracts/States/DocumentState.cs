namespace StockSharp.DesktopDriver.States;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// What a document is showing.
/// </summary>
/// <param name="DocumentKind">What sort of document it is - code, markdown, plain text.</param>
/// <param name="Language">The language it is highlighted as, when it is.</param>
/// <param name="LineCount">How many lines it holds.</param>
/// <param name="CharacterCount">How many characters.</param>
/// <param name="CaretLine">Which line the caret is on, counting from one.</param>
/// <param name="CaretColumn">Which column, counting from one.</param>
/// <param name="SelectionLength">How much is selected, in characters.</param>
/// <param name="IsReadOnly">Whether it refuses changes.</param>
/// <param name="Problems">What the editor is complaining about, in its own words.</param>
/// <remarks>
/// The text itself is not here. A document is read one page of lines at a time, because a state that
/// carried the whole of it would be unbounded and one that carried some of it would be a lie about
/// the rest.
/// </remarks>
public sealed record DocumentState(
	string DocumentKind,
	UiField<string> Language,
	UiField<long> LineCount,
	UiField<long> CharacterCount,
	UiField<long> CaretLine,
	UiField<long> CaretColumn,
	UiField<long> SelectionLength,
	bool IsReadOnly,
	ImmutableArray<string> Problems) : UiState
{
	/// <inheritdoc />
	public override string Kind => UiStateKinds.Document;
}
