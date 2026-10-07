namespace StockSharp.DesktopDriver.States;

/// <summary>
/// One column the grid is grouped by.
/// </summary>
/// <param name="ColumnId">The column.</param>
/// <param name="Level">Its nesting level; zero is the outermost.</param>
public sealed record GridGrouping(string ColumnId, int Level);
