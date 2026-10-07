namespace StockSharp.DesktopDriver.Session;

using StockSharp.DesktopDriver.Queries;

/// <summary>
/// How an application opens its endpoint.
/// </summary>
/// <param name="AppId">The product.</param>
/// <param name="FixtureId">The fixture it was started with.</param>
/// <param name="SafeTestProfile">Whether its outside world has been replaced.</param>
/// <param name="DefaultBudget">How much a reply may carry when the caller does not say.</param>
/// <param name="MaxActions">How many actions one session may perform.</param>
public sealed record UiHostOptions(
	string AppId,
	string FixtureId,
	bool SafeTestProfile,
	UiReadBudget DefaultBudget,
	int MaxActions)
{
	/// <summary>
	/// The defaults for a product that says only its name.
	/// </summary>
	/// <param name="appId">The product.</param>
	/// <returns>The options.</returns>
	public static UiHostOptions ForApp(string appId)
		=> new(appId, "default", true, UiReadBudget.Default, 10000);
}
