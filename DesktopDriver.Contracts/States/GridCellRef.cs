namespace StockSharp.DesktopDriver.States;

/// <summary>
/// One cell, named by its record and its column.
/// </summary>
/// <param name="RowKey">The record's stable key.</param>
/// <param name="ColumnId">The column.</param>
/// <remarks>
/// Named by key rather than by index because an index means a different record after a sort, and a test
/// that clicks "row 3" after sorting has clicked something it never looked at.
/// </remarks>
public sealed record GridCellRef(string RowKey, string ColumnId);
