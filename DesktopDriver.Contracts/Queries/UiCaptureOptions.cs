namespace StockSharp.DesktopDriver.Queries;

using System.Collections.Immutable;

using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// How much of a node to read.
/// </summary>
/// <param name="Fields">The schema fields to fill in; empty means the documented summary.</param>
/// <param name="IncludeLayout">Whether geometry is wanted.</param>
/// <param name="Budget">How much the reply may carry.</param>
/// <param name="Guard">What the caller believed, so a changed answer can be refused.</param>
/// <remarks>
/// The field list is a whitelist of known schema paths, not an expression: nothing here selects arbitrary
/// members of arbitrary objects, and asking for an unknown path is a mistake in the request rather than a
/// licence to reflect over the control.
/// </remarks>
public sealed record UiCaptureOptions(
	ImmutableArray<string> Fields,
	bool IncludeLayout,
	UiReadBudget Budget,
	UiReadGuard Guard)
{
	/// <summary>
	/// The summary of a node, with geometry, within the default budget.
	/// </summary>
	public static UiCaptureOptions Default { get; } =
		new(ImmutableArray<string>.Empty, true, UiReadBudget.Default, null);
}
