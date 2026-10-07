namespace StockSharp.DesktopDriver.Diagnostics;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;

/// <summary>
/// One thing to keep out of a picture.
/// </summary>
/// <param name="Node">What it belongs to.</param>
/// <param name="Part">Which part of it, or <see langword="null"/> for the whole of it.</param>
/// <param name="Scope">Whether the node itself is covered or its text, wherever that text is drawn.</param>
/// <remarks>
/// A part is named the same way an action names one, and answered by the same adapter - so "the account
/// column of that table" means here exactly what it means when something is clicked. A column is covered
/// down the whole height of its table rather than only where its header is: what has to be left out of a
/// picture is the values, and the values are the rest of the column.
/// <para>
/// <see cref="UiMaskScopes.Text"/> is for a value that is repeated where no control of its own holds it:
/// the name of whoever is signed in is shown in a status bar and quoted again in the middle of a line of
/// the log. It names no part.
/// </para>
/// </remarks>
public sealed record UiMask(UiNodeId Node, UiTargetPart Part, UiMaskScopes Scope);
