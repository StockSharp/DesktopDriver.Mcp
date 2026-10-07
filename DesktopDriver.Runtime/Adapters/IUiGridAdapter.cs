namespace StockSharp.DesktopDriver.Adapters;

using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.States;

/// <summary>
/// Reads a grid: its columns, the rows it is showing, and its groups.
/// </summary>
public interface IUiGridAdapter : IUiSnapshotAdapter
{
	/// <summary>
	/// Reads the columns.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="query">Which columns.</param>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>One page of columns.</returns>
	UiDataPage<GridColumnSnapshot> ReadColumns(
		UiSubject subject,
		GridColumnsQuery query,
		UiCaptureContext context);

	/// <summary>
	/// Reads the rows the grid is showing, in the order it is showing them.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="query">Which rows.</param>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>One page of rows.</returns>
	/// <remarks>
	/// The order is the grid's own, after its sorting and grouping. The collection it is bound to is not
	/// an answer to what is on screen; a test that checks a sort against the source checks nothing.
	/// </remarks>
	UiDataPage<GridRowSnapshot> ReadRows(
		UiSubject subject,
		GridRowsQuery query,
		UiCaptureContext context);

	/// <summary>
	/// Reads the groups.
	/// </summary>
	/// <param name="subject">The node and its instance.</param>
	/// <param name="query">Which groups.</param>
	/// <param name="context">What is being read and how much of it.</param>
	/// <returns>One page of groups.</returns>
	UiDataPage<GridGroupSnapshot> ReadGroups(
		UiSubject subject,
		GridGroupsQuery query,
		UiCaptureContext context);
}
