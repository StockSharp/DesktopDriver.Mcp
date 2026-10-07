namespace StockSharp.DesktopDriver.States;

using StockSharp.DesktopDriver.Values;

/// <summary>
/// What a workspace holds, in summary.
/// </summary>
/// <param name="WorkspaceId">The workspace.</param>
/// <param name="GroupCount">How many layout groups it has.</param>
/// <param name="PanelCount">How many panels it has.</param>
/// <param name="FocusedPanelId">Which panel has the focus; a known <see langword="null"/> means none does.</param>
public sealed record DockState(
	string WorkspaceId,
	UiField<long> GroupCount,
	UiField<long> PanelCount,
	UiField<string> FocusedPanelId) : UiState
{
	/// <inheritdoc />
	public override string Kind => UiStateKinds.Dock;
}
