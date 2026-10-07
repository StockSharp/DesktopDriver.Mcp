namespace StockSharp.DesktopDriver.Snapshots;

using System;

/// <summary>
/// A version of something observable, valid only within its epoch.
/// </summary>
/// <param name="Epoch">Changes when the source itself is replaced.</param>
/// <param name="Version">Increases as that source changes.</param>
/// <remarks>
/// Two versions are only comparable inside the same epoch. When a grid is rebound to a different
/// collection its numbering starts over, and a test that compared the numbers across that boundary would
/// conclude the data went backwards.
/// </remarks>
public sealed record UiRevision(Guid Epoch, long Version)
{
	/// <summary>
	/// Whether this revision comes after another one of the same source.
	/// </summary>
	/// <param name="baseline">The earlier revision.</param>
	/// <returns><see langword="true"/> when the epoch matches and the version is higher.</returns>
	public bool IsAfter(UiRevision baseline)
		=> baseline is not null && Epoch == baseline.Epoch && Version > baseline.Version;
}
