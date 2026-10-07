namespace StockSharp.DesktopDriver.Testing;

using System;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Client;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// What a test asks for when it wants a product running.
/// </summary>
/// <param name="AppId">The product.</param>
/// <param name="RecipeId">Which registered way of starting it.</param>
/// <param name="FixtureId">The fixture it should show.</param>
/// <param name="RunId">The run this belongs to.</param>
/// <param name="TestId">The test asking.</param>
/// <param name="Culture">The display language to start it in, or <see langword="null"/> for the default.</param>
/// <remarks>
/// A recipe is named, never described. Letting a request carry an executable and its arguments would
/// make this a way to run anything on the machine, and the protocol above it is reachable by an agent.
/// </remarks>
public sealed record UiLaunchRequest(
	string AppId,
	string RecipeId,
	string FixtureId,
	Guid RunId,
	string TestId,
	string Culture);

/// <summary>
/// A product running for one test.
/// </summary>
public interface IUiTestApplication : IAsyncDisposable
{
	/// <summary>
	/// The channel to it.
	/// </summary>
	UiAutomationClient Ui { get; }

	/// <summary>
	/// What it says about itself.
	/// </summary>
	UiSessionInfo Session { get; }

	/// <summary>
	/// Where this test's artifacts go: its result, its steps, and any picture it kept.
	/// </summary>
	string ArtifactDirectory { get; }

	/// <summary>
	/// Closes it.
	/// </summary>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>A task that completes when it is gone.</returns>
	Task StopAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Starts products for tests.
/// </summary>
public interface IUiTestApplicationFactory
{
	/// <summary>
	/// Starts one.
	/// </summary>
	/// <param name="request">What to start.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The running product.</returns>
	/// <exception cref="InvalidOperationException">No such recipe, or it names a product other than the one asked for.</exception>
	Task<IUiTestApplication> StartAsync(UiLaunchRequest request, CancellationToken cancellationToken);
}
