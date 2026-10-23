namespace StockSharp.DesktopDriver.Diagnostics;

using System;
using System.Collections.Generic;
using System.Linq;

using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Where the frames of a picture are drawn.
/// </summary>
public static class UiFrames
{
	/// <summary>
	/// The places to frame, with every two whose frames would meet joined into one place round both.
	/// </summary>
	/// <param name="places">Where each framed thing is.</param>
	/// <param name="reach">How far outside its place the outer edge of a frame runs.</param>
	/// <returns>The places, in the order the first of each was given.</returns>
	/// <remarks>
	/// The tabs of a strip and the commands of a toolbar stand edge to edge, and framed one by one every frame would cut
	/// through its neighbours'. A place that lies inside another keeps a frame of its own: a button framed inside the
	/// framed panel it stands in.
	/// </remarks>
	public static IReadOnlyList<UiRect> Merged(IReadOnlyList<UiRect> places, double reach)
	{
		ArgumentNullException.ThrowIfNull(places);

		var merged = places.ToList();

		for (var joined = true; joined;)
		{
			joined = false;

			for (var first = 0; first < merged.Count && !joined; first++)
			{
				for (var second = first + 1; second < merged.Count; second++)
				{
					if (!Meet(merged[first], merged[second], reach) || Holds(merged[first], merged[second]) || Holds(merged[second], merged[first]))
						continue;

					merged[first] = Union(merged[first], merged[second]);
					merged.RemoveAt(second);
					joined = true;

					break;
				}
			}
		}

		return merged;
	}

	private static bool Meet(UiRect first, UiRect second, double reach)
		=> first.X - reach <= second.Right + reach
			&& second.X - reach <= first.Right + reach
			&& first.Y - reach <= second.Bottom + reach
			&& second.Y - reach <= first.Bottom + reach;

	private static bool Holds(UiRect outer, UiRect inner)
		=> outer.X <= inner.X && outer.Y <= inner.Y && outer.Right >= inner.Right && outer.Bottom >= inner.Bottom;

	private static UiRect Union(UiRect first, UiRect second)
	{
		var x = Math.Min(first.X, second.X);
		var y = Math.Min(first.Y, second.Y);

		return new(x, y, Math.Max(first.Right, second.Right) - x, Math.Max(first.Bottom, second.Bottom) - y);
	}
}
