namespace StockSharp.DesktopDriver.Host;

/// <summary>
/// What an application says about itself when it opens its endpoint.
/// </summary>
/// <param name="AppId">The product's stable identifier, the one a runner names.</param>
/// <param name="AppVersion">The product's version.</param>
/// <param name="FixtureId">The fixture it is showing, or <see langword="null"/> when it is showing none.</param>
/// <param name="SafeTestProfile">Whether nothing it can be made to do reaches the outside world.</param>
public sealed record UiAutomationStartup(
	string AppId,
	string AppVersion,
	string FixtureId,
	bool SafeTestProfile)
{
	/// <summary>
	/// Works out what this run may honestly claim.
	/// </summary>
	/// <param name="appId">The product's stable identifier.</param>
	/// <param name="appVersion">The product's version.</param>
	/// <param name="fixtureId">The fixture the product shows when it is started for a test.</param>
	/// <param name="hasLiveOutsideWorld">Whether the product connects to anything real at all.</param>
	/// <param name="isTestProfile">Whether this run was started on a test profile.</param>
	/// <returns>The startup description.</returns>
	/// <remarks>
	/// Worked out rather than stated, because a caller that could state it would state it wrongly. Every
	/// application used to pass a literal "the outside world has been replaced", so an ordinary Terminal
	/// on a live account told every runner that nothing it did could reach anything - and that is the one
	/// claim a runner acts on before it starts clicking.
	/// <para>
	/// A fixture is what a test profile puts in front of the product. A run that has none names none:
	/// naming one anyway would tell a runner its data had been arranged for it.
	/// </para>
	/// </remarks>
	public static UiAutomationStartup For(
		string appId,
		string appVersion,
		string fixtureId,
		bool hasLiveOutsideWorld,
		bool isTestProfile)
	{
		var safe = !hasLiveOutsideWorld || isTestProfile;

		return new(appId, appVersion, safe ? fixtureId : null, safe);
	}
}
