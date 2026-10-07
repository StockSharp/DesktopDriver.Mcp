namespace StockSharp.DesktopDriver.States;

using System;

using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// How much a control says it is showing.
/// </summary>
/// <remarks>
/// A reader that only looks at whether a control exists cannot tell a table of a hundred records from the
/// same table before anything arrived in it. Every state that counts anything counts it under a different
/// name - rows, series, levels, lines - and this is the one question asked across all of them.
/// </remarks>
public static class UiShowing
{
	/// <summary>
	/// How much one control is showing.
	/// </summary>
	/// <param name="state">What the control holds.</param>
	/// <returns>The count, or <see langword="null"/> for a control that counts nothing.</returns>
	/// <remarks>
	/// A grid answers with what is left after filtering, because that is what the reader sees, and falls
	/// back to what it has fetched when nothing is filtered. A control that counts nothing - a button, a
	/// panel, an editor of a single value - answers <see langword="null"/> rather than zero: it is not
	/// empty, it is not that kind of control.
	/// </remarks>
	public static long? Showing(this UiState state)
		=> state switch
		{
			GridState grid => Known(grid.Counts.Filtered) ?? Known(grid.Counts.Loaded),
			ChartState chart => Known(chart.SeriesCount),
			OrderBookState book => Known(book.BidCount) + Known(book.AskCount),
			TreeState tree => Known(tree.VisibleItemCount),
			ListState list => Known(list.ItemCount),
			DocumentState document => Known(document.LineCount),
			PropertyEditorState editor => Known(editor.ItemCount),
			DiagramState diagram => Known(diagram.NodeCount),
			_ => null,
		};

	/// <summary>
	/// How much a control and everything inside it is showing.
	/// </summary>
	/// <param name="tree">The subtree, as a walk answered.</param>
	/// <returns>The count, or <see langword="null"/> when nothing in it counts anything.</returns>
	/// <remarks>
	/// The largest of what anything in there counts, rather than their sum or the least of them: the
	/// question a caller asks of a panel is whether it has anything to show, and one filled table answers
	/// that even though the sparkline beside it has drawn no series and the toolbar counts nothing at all.
	/// </remarks>
	public static long? Showing(this UiTreeSnapshot tree)
	{
		ArgumentNullException.ThrowIfNull(tree);

		long? most = null;

		foreach (var node in tree.Nodes)
		{
			if (node.State.Showing() is not { } showing)
				continue;

			if (most is null || showing > most)
				most = showing;
		}

		return most;
	}

	private static long? Known(UiField<long> field)
		=> field is UiKnown<long> known ? known.Value : null;
}
