namespace StockSharp.DesktopDriver.States;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// What a Studio panel is.
/// </summary>
/// <param name="LayoutKey">The key the layout remembers it by.</param>
/// <param name="Title">What it calls itself, which several panels compose from what they are showing.</param>
/// <param name="PanelKind">Its type, which is what a test names it by.</param>
/// <param name="SavesWithLayout">Whether the layout keeps it between runs.</param>
/// <param name="BoundTo">The view model behind it, or nothing when it has not been given one.</param>
/// <remarks>
/// Deliberately says nothing about what is inside. The table, chart or book a panel holds is a node of
/// its own and is read by whichever adapter knows that control - which is why a table never has to know
/// it is in a panel, and why the same table reads the same way in every product.
/// </remarks>
public sealed record PanelState(
	string LayoutKey,
	UiField<string> Title,
	UiField<string> PanelKind,
	UiField<bool> SavesWithLayout,
	UiField<string> BoundTo) : UiState
{
	/// <inheritdoc />
	public override string Kind => UiStateKinds.Panel;
}
