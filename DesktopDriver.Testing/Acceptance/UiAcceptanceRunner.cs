namespace StockSharp.DesktopDriver.Testing;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Serialization;

/// <summary>
/// One scenario of the matrix: a product, the way it is started, and what to do to it.
/// </summary>
/// <param name="TestId">What the scenario is called.</param>
/// <param name="AppId">The product.</param>
/// <param name="RecipeId">Which registered way of starting it.</param>
/// <param name="FixtureId">The fixture it should show.</param>
/// <param name="Body">What to do once it is up.</param>
public sealed record UiAcceptanceScenario(
	string TestId,
	string AppId,
	string RecipeId,
	string FixtureId,
	Func<IUiTestApplication, CancellationToken, Task> Body);

/// <summary>
/// Runs the matrix and writes down what happened, stage by stage.
/// </summary>
/// <param name="factory">How a product is started.</param>
/// <param name="artifactRoot">Where the run's artifacts go.</param>
/// <remarks>
/// The point of running the matrix rather than a list of tests is that the answer has to be able to say
/// "this was never tried". A runner whose only outcomes are pass and fail reports an unbuilt product, an
/// unregistered recipe and a desktop that refuses input all as nothing at all - and the run goes green
/// with the products that matter most untested.
/// </remarks>
public sealed class UiAcceptanceRunner(IUiTestApplicationFactory factory, string artifactRoot)
{
	private const string _launch = "launch";
	private const string _handshake = "handshake";
	private const string _scenario = "scenario";
	private const string _artifacts = "artifacts";
	private const string _shutdown = "shutdown";

	private readonly IUiTestApplicationFactory _factory = factory ?? throw new ArgumentNullException(nameof(factory));
	private readonly string _artifactRoot = artifactRoot ?? throw new ArgumentNullException(nameof(artifactRoot));

	/// <summary>
	/// Runs every scenario, one at a time.
	/// </summary>
	/// <param name="scenarios">What to run.</param>
	/// <param name="runId">The run.</param>
	/// <param name="startedAt">When the run started, UTC.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The report.</returns>
	/// <remarks>
	/// One at a time on purpose: these drive a real desktop, and two of them at once would send input to
	/// each other's windows.
	/// </remarks>
	public async Task<UiAcceptanceReport> RunAsync(
		IEnumerable<UiAcceptanceScenario> scenarios,
		Guid runId,
		DateTime startedAt,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(scenarios);

		var cases = ImmutableArray.CreateBuilder<UiAcceptanceCase>();

		foreach (var scenario in scenarios)
			cases.Add(await RunAsync(scenario, runId, cancellationToken).ConfigureAwait(false));

		var report = new UiAcceptanceReport(runId, startedAt, DateTime.UtcNow, cases.ToImmutable());

		Write(Path.Combine(_artifactRoot, runId.ToString("N"), "report.json"), report);

		return report;
	}

	private async Task<UiAcceptanceCase> RunAsync(
		UiAcceptanceScenario scenario,
		Guid runId,
		CancellationToken cancellationToken)
	{
		var steps = ImmutableArray.CreateBuilder<UiAcceptanceStep>();
		var clock = Stopwatch.StartNew();

		IUiTestApplication application = null;

		try
		{
			try
			{
				application = await _factory
					.StartAsync(
						new UiLaunchRequest(
							scenario.AppId, scenario.RecipeId, scenario.FixtureId, runId, scenario.TestId, null),
						cancellationToken)
					.ConfigureAwait(false);

				steps.Add(new UiAcceptanceStep(_launch, UiAcceptanceOutcomes.Passed, null));
			}
			catch (Exception error)
			{
				// Nothing about the product was established, so this is not a failure of the product. It is
				// a case that could not be attempted, and calling it anything else loses that.
				steps.Add(new UiAcceptanceStep(_launch, UiAcceptanceOutcomes.Blocked, error.Message));

				return Done(scenario, runId, steps, clock, UiAcceptanceOutcomes.Blocked, null);
			}

			if (application.Session is null || application.Session.AppId != scenario.AppId)
			{
				steps.Add(new UiAcceptanceStep(
					_handshake,
					UiAcceptanceOutcomes.Failed,
					$"The endpoint answered as {application.Session?.AppId ?? "nothing"}."));

				return Done(scenario, runId, steps, clock, UiAcceptanceOutcomes.Failed, application.ArtifactDirectory);
			}

			steps.Add(new UiAcceptanceStep(_handshake, UiAcceptanceOutcomes.Passed, null));

			try
			{
				await scenario.Body(application, cancellationToken).ConfigureAwait(false);

				steps.Add(new UiAcceptanceStep(_scenario, UiAcceptanceOutcomes.Passed, null));
			}
			catch (UiAutomationException error) when (error.Error.Code == UiErrorCodes.InputBlocked)
			{
				// The desktop refused the input before anything reached the application, so nothing about
				// the application was established here either.
				steps.Add(new UiAcceptanceStep(_scenario, UiAcceptanceOutcomes.Blocked, error.Error.Message));

				return Done(scenario, runId, steps, clock, UiAcceptanceOutcomes.Blocked, application.ArtifactDirectory);
			}
			catch (Exception error)
			{
				steps.Add(new UiAcceptanceStep(_scenario, UiAcceptanceOutcomes.Failed, error.Message));

				return Done(scenario, runId, steps, clock, UiAcceptanceOutcomes.Failed, application.ArtifactDirectory);
			}

			steps.Add(new UiAcceptanceStep(
				_artifacts,
				Directory.Exists(application.ArtifactDirectory)
					? UiAcceptanceOutcomes.Passed
					: UiAcceptanceOutcomes.Failed,
				application.ArtifactDirectory));

			return Done(scenario, runId, steps, clock, UiAcceptanceOutcomes.Passed, application.ArtifactDirectory);
		}
		finally
		{
			if (application is not null)
			{
				try
				{
					await application.StopAsync(cancellationToken).ConfigureAwait(false);

					steps.Add(new UiAcceptanceStep(_shutdown, UiAcceptanceOutcomes.Passed, null));
				}
				catch (Exception error)
				{
					steps.Add(new UiAcceptanceStep(_shutdown, UiAcceptanceOutcomes.Failed, error.Message));
				}
			}
		}
	}

	private UiAcceptanceCase Done(
		UiAcceptanceScenario scenario,
		Guid runId,
		ImmutableArray<UiAcceptanceStep>.Builder steps,
		Stopwatch clock,
		UiAcceptanceOutcomes outcome,
		string artifacts)
	{
		var directory = artifacts ?? Path.Combine(_artifactRoot, runId.ToString("N"), scenario.TestId);
		var result = new UiAcceptanceCase(
			scenario.TestId, scenario.AppId, scenario.RecipeId, outcome, steps.ToImmutable(), directory, clock.Elapsed);

		Write(Path.Combine(directory, "result.json"), result);
		Write(Path.Combine(directory, "steps.json"), result.Steps);

		return result;
	}

	// Written next to whatever else the case kept, so that a failure is read from files rather than from
	// a console that has scrolled away by the time anybody looks.
	private static void Write<T>(string path, T value)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path, UiJson.Write(value));
		}
		catch (IOException)
		{
			// Losing the record of a run is worth less than the run itself, and a disk that will not take
			// it has already made itself known.
		}
	}
}
