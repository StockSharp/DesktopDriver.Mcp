namespace StockSharp.DesktopDriver.Testing;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Client;
using StockSharp.DesktopDriver.Runner;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// Starts a product from a registered recipe and hands back a channel to it.
/// </summary>
/// <param name="artifactRoot">Where each test's artifacts are kept.</param>
/// <param name="patience">How long a product may take to open its endpoint.</param>
public sealed class UiTestApplicationFactory(string artifactRoot, TimeSpan patience) : IUiTestApplicationFactory
{
	private readonly string _artifactRoot = artifactRoot ?? throw new ArgumentNullException(nameof(artifactRoot));
	private readonly TimeSpan _patience = patience;

	/// <inheritdoc />
	public async Task<IUiTestApplication> StartAsync(UiLaunchRequest request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (!UiLaunchRecipes.TryGet(request.RecipeId, out var recipe, out var owner))
		{
			throw new InvalidOperationException(
				$"'{request.RecipeId}' is not a registered way of starting anything. A request names a recipe; " +
				"it does not describe one.");
		}

		if (!string.IsNullOrEmpty(request.AppId) && request.AppId != recipe.AppId)
		{
			throw new InvalidOperationException(
				$"'{request.RecipeId}' starts {recipe.AppId}, and the request asked for {request.AppId}.");
		}

		var artifacts = Path.Combine(_artifactRoot, request.RunId.ToString("N"), Safe(request.TestId));

		Directory.CreateDirectory(artifacts);

		var executable = UiLaunchRecipes.ExecutableOf(recipe, owner);
		var profile = Path.Combine(artifacts, "profile");

		if (!recipe.UsesTestProfile && !string.IsNullOrEmpty(request.Culture))
		{
			throw new InvalidOperationException(
				$"'{recipe.RecipeId}' starts without a test profile, and the display language is part of one.");
		}

		var arguments = recipe.UsesTestProfile
			? $"--ui-test --ui-test-dir={profile}"
			: null;

		if (!string.IsNullOrEmpty(request.Culture))
			arguments = $"{arguments} --ui-test-lang={request.Culture}";

		var driven = await DrivenApplication
			.StartAsync(executable, arguments, _patience, cancellationToken)
			.ConfigureAwait(false);

		try
		{
			var client = await driven.ConnectAsync(recipe.AppId, cancellationToken).ConfigureAwait(false);
			var session = await client.GetSessionAsync(cancellationToken).ConfigureAwait(false);

			return new Running(driven, client, session, artifacts);
		}
		catch (Exception)
		{
			await driven.DisposeAsync().ConfigureAwait(false);

			throw;
		}
	}

	// A test identifier goes into a directory name, and a test is named by a person.
	private static string Safe(string testId)
	{
		if (string.IsNullOrEmpty(testId))
			return "test";

		var name = testId;

		foreach (var invalid in Path.GetInvalidFileNameChars())
			name = name.Replace(invalid, '-');

		return name;
	}

	private sealed class Running(
		DrivenApplication driven,
		UiAutomationClient client,
		UiSessionInfo session,
		string artifacts) : IUiTestApplication
	{
		public UiAutomationClient Ui { get; } = client;

		public UiSessionInfo Session { get; } = session;

		public string ArtifactDirectory { get; } = artifacts;

		public async Task StopAsync(CancellationToken cancellationToken)
		{
			await Ui.DisposeAsync().ConfigureAwait(false);
			await driven.DisposeAsync().ConfigureAwait(false);
		}

		public ValueTask DisposeAsync() => new(StopAsync(CancellationToken.None));
	}
}
