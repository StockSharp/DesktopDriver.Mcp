namespace StockSharp.DesktopDriver.Testing;

using System;
using System.Collections.Immutable;
using System.Linq;

/// <summary>
/// How one case of the matrix ended.
/// </summary>
/// <remarks>
/// Five outcomes rather than two, because the three that are not pass or fail are the ones a run hides
/// when it has nowhere to put them. A case that never ran and a case that passed look the same in a
/// count of failures, and that is exactly how a missing product becomes a green run.
/// </remarks>
public enum UiAcceptanceOutcomes
{
	/// <summary>It ran and every check held.</summary>
	Passed,

	/// <summary>It ran and something did not hold.</summary>
	Failed,

	/// <summary>It was never attempted.</summary>
	NotRun,

	/// <summary>It could not be attempted here: no recipe, no desktop, nothing built.</summary>
	Blocked,

	/// <summary>It does not apply to this product.</summary>
	NotApplicable,
}

/// <summary>
/// One stage of one case.
/// </summary>
/// <param name="Name">What the stage is.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Detail">What happened, when that is worth saying.</param>
/// <remarks>
/// Stages are reported separately because they fail for different reasons and are fixed by different
/// people: a product that will not start, a handshake that is refused and a scenario whose assertion
/// does not hold are three different problems, and one verdict for all three names none of them.
/// </remarks>
public sealed record UiAcceptanceStep(string Name, UiAcceptanceOutcomes Outcome, string Detail);

/// <summary>
/// One product driven through one scenario.
/// </summary>
/// <param name="TestId">The scenario.</param>
/// <param name="AppId">The product.</param>
/// <param name="RecipeId">How it was started.</param>
/// <param name="Outcome">How the case ended.</param>
/// <param name="Steps">Each stage of it.</param>
/// <param name="ArtifactDirectory">Where what it kept was written.</param>
/// <param name="Elapsed">How long it took.</param>
public sealed record UiAcceptanceCase(
	string TestId,
	string AppId,
	string RecipeId,
	UiAcceptanceOutcomes Outcome,
	ImmutableArray<UiAcceptanceStep> Steps,
	string ArtifactDirectory,
	TimeSpan Elapsed);

/// <summary>
/// What a whole matrix run came to.
/// </summary>
/// <param name="RunId">The run.</param>
/// <param name="StartedAt">When it started, UTC.</param>
/// <param name="FinishedAt">When it finished, UTC.</param>
/// <param name="Cases">Every case.</param>
public sealed record UiAcceptanceReport(
	Guid RunId,
	DateTime StartedAt,
	DateTime FinishedAt,
	ImmutableArray<UiAcceptanceCase> Cases)
{
	/// <summary>
	/// How many cases ended each way.
	/// </summary>
	/// <param name="outcome">The outcome.</param>
	/// <returns>The count.</returns>
	public int Count(UiAcceptanceOutcomes outcome) => Cases.Count(item => item.Outcome == outcome);

	/// <summary>
	/// Whether the run may be reported as a success.
	/// </summary>
	/// <remarks>
	/// Only passes and cases that genuinely do not apply. A case that was blocked or never ran leaves the
	/// run un-green on purpose: it is the one thing a runner must not be able to round off, because
	/// rounding it off is how an untested product ships.
	/// </remarks>
	public bool IsGreen
		=> Cases.Length > 0 &&
			Cases.All(item => item.Outcome is UiAcceptanceOutcomes.Passed or UiAcceptanceOutcomes.NotApplicable) &&
			Cases.Any(item => item.Outcome == UiAcceptanceOutcomes.Passed);

	/// <summary>
	/// A one-line summary for a log.
	/// </summary>
	/// <returns>The summary.</returns>
	public override string ToString()
		=> $"{Count(UiAcceptanceOutcomes.Passed)} passed, {Count(UiAcceptanceOutcomes.Failed)} failed, " +
			$"{Count(UiAcceptanceOutcomes.Blocked)} blocked, {Count(UiAcceptanceOutcomes.NotRun)} not run, " +
			$"{Count(UiAcceptanceOutcomes.NotApplicable)} not applicable";
}
