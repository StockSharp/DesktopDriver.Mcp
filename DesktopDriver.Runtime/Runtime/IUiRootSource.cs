namespace StockSharp.DesktopDriver.Runtime;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Where a walk of the interface starts.
/// </summary>
public interface IUiRootSource
{
	/// <summary>
	/// The topmost nodes.
	/// </summary>
	/// <param name="budget">How many may be returned.</param>
	/// <returns>The roots.</returns>
	ImmutableArray<UiSubject> GetRoots(UiReadBudget budget);

	/// <summary>
	/// The windows and popups currently on screen.
	/// </summary>
	/// <returns>The surfaces.</returns>
	ImmutableArray<UiSurfaceInfo> GetSurfaces();
}
