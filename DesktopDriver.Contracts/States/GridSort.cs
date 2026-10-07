namespace StockSharp.DesktopDriver.States;

/// <summary>
/// One column taking part in the sort.
/// </summary>
/// <param name="ColumnId">The column.</param>
/// <param name="Direction">Which way.</param>
/// <param name="Priority">Its place in the sort; zero is the primary key.</param>
public sealed record GridSort(string ColumnId, UiSortDirections Direction, int Priority);
