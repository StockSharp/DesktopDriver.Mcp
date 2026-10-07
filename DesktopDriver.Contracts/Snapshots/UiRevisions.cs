namespace StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// The three things about a node that change independently.
/// </summary>
/// <param name="State">What the node holds.</param>
/// <param name="View">What it shows and in what order, after sorting, filtering and grouping.</param>
/// <param name="Layout">Where it is and how big.</param>
/// <remarks>
/// Kept apart because tests wait on them separately: sorting a grid changes the view without changing the
/// data, and floating a panel changes the layout without touching either.
/// </remarks>
public sealed record UiRevisions(UiRevision State, UiRevision View, UiRevision Layout);
