namespace StockSharp.DesktopDriver.States;

/// <summary>
/// One line of a document.
/// </summary>
/// <param name="Number">Its line number, counting from one, as the editor numbers it.</param>
/// <param name="Text">What is on it, without its line ending.</param>
/// <remarks>
/// Numbered from one because that is what the editor's own margin shows: a test that read line 12 and
/// an editor that showed line 12 have to mean the same line.
/// </remarks>
public sealed record DocumentLineSnapshot(long Number, string Text);
