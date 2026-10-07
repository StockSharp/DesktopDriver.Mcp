namespace DesktopDriver.Sample;

/// <summary>
/// One row of the sample's table.
/// </summary>
/// <param name="Symbol">The ticker.</param>
/// <param name="Exchange">Where it trades.</param>
/// <param name="Price">The last price.</param>
/// <param name="Volume">The volume traded today.</param>
public sealed record Instrument(string Symbol, string Exchange, decimal Price, long Volume);
