namespace StockSharp.DesktopDriver.States;

using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// What a diagram is showing.
/// </summary>
/// <param name="NodeCount">How many blocks are drawn.</param>
/// <param name="ConnectionCount">How many connections between them.</param>
/// <param name="SelectedNodeCount">How many blocks are selected.</param>
/// <param name="SelectedNodeKey">The selected block, when exactly one is.</param>
/// <param name="Scale">How far the surface is zoomed in.</param>
/// <param name="IsEditable">Whether the diagram accepts changes.</param>
/// <param name="HasErrors">Whether the composition itself is broken.</param>
/// <param name="ContentBoundsInSurfaceDip">The area the drawn blocks cover.</param>
/// <remarks>
/// Counted from what the surface drew rather than from the composition behind it. A block the model
/// holds but the surface never presented is exactly the fault a test about a diagram is looking for.
/// </remarks>
public sealed record DiagramState(
	UiField<long> NodeCount,
	UiField<long> ConnectionCount,
	UiField<long> SelectedNodeCount,
	UiField<string> SelectedNodeKey,
	UiField<double> Scale,
	bool IsEditable,
	bool HasErrors,
	UiField<UiRect> ContentBoundsInSurfaceDip) : UiState
{
	/// <inheritdoc />
	public override string Kind => UiStateKinds.Diagram;
}
