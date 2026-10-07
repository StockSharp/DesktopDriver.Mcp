namespace StockSharp.DesktopDriver.Queries;

/// <summary>
/// How much one read is allowed to return.
/// </summary>
/// <param name="MaxDepth">How far down the tree to walk.</param>
/// <param name="MaxNodes">How many nodes one reply may carry.</param>
/// <param name="MaxItems">How many rows, points or children one page may carry.</param>
/// <param name="MaxBytes">How large the serialised reply may be.</param>
/// <remarks>
/// The budget exists because the caller is often a language model paying by the token, and because a grid
/// of a million rows must not be materialised to answer how many rows it has. Every limit that bites is
/// reported rather than applied quietly.
/// </remarks>
public sealed record UiReadBudget(
	int MaxDepth = 4,
	int MaxNodes = 200,
	int MaxItems = 50,
	int MaxBytes = 262144)
{
	/// <summary>
	/// The defaults.
	/// </summary>
	public static UiReadBudget Default { get; } = new();

	/// <summary>
	/// Enough to walk a panel down to the control that holds its data.
	/// </summary>
	/// <remarks>
	/// The default depth of four reaches the parts of a window, not the chart at the bottom of a docked
	/// panel built from a host, a view, a layout and a presenter. A walk that stopped short would report
	/// the panel as holding nothing, which reads exactly like a panel that is empty.
	/// </remarks>
	public static UiReadBudget Deep { get; } = new(MaxDepth: 24, MaxNodes: 2000, MaxItems: 50, MaxBytes: 4 * 1024 * 1024);
}
