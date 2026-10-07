namespace StockSharp.DesktopDriver.Tests.Protocol;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Host;

/// <summary>
/// What an application may honestly claim about itself when it opens its endpoint.
/// </summary>
/// <remarks>
/// A runner reads the safe-profile flag before it starts clicking, and acts on it. A product that
/// claims its outside world has been replaced, while it is the ordinary copy somebody uses against a
/// live account, invites a test to place a real order.
/// <para>
/// Checked here rather than by starting a product without a test profile, because starting one that
/// way is the very thing that would read and write the settings of whoever is logged in.
/// </para>
/// </remarks>
[TestClass]
public class SessionClaimTests : BaseTestClass
{
	[TestMethod]
	public void AProductWithALiveOutsideWorldIsNotSafeUntilAProfileReplacesIt()
	{
		var started = UiAutomationStartup.For(
			"stocksharp.terminal", "5.0", "terminal.default", hasLiveOutsideWorld: true, isTestProfile: false);

		IsFalse(started.SafeTestProfile);
	}

	[TestMethod]
	public void TheSameProductOnATestProfileIsSafe()
	{
		var started = UiAutomationStartup.For(
			"stocksharp.terminal", "5.0", "terminal.default", hasLiveOutsideWorld: true, isTestProfile: true);

		IsTrue(started.SafeTestProfile);
		AreEqual("terminal.default", started.FixtureId);
	}

	[TestMethod]
	public void AnApplicationWithNoOutsideWorldIsSafeEitherWay()
	{
		// A sample shows what its own process made up. There is nothing to replace, so there is nothing
		// a test profile could make safer.
		foreach (var profile in new[] { false, true })
		{
			var started = UiAutomationStartup.For(
				"stocksharp.controls-showcase", "5.0", "showcase.default",
				hasLiveOutsideWorld: false, isTestProfile: profile);

			IsTrue(started.SafeTestProfile, $"profile: {profile}");
			AreEqual("showcase.default", started.FixtureId);
		}
	}

	[TestMethod]
	public void ARunThatIsNotOnAFixtureNamesNone()
	{
		// Naming one would tell a runner the data in front of it had been arranged for it, and it would
		// then compare against values nobody put there.
		var started = UiAutomationStartup.For(
			"stocksharp.hydra", "5.0", "hydra.default", hasLiveOutsideWorld: true, isTestProfile: false);

		IsNull(started.FixtureId);
	}

}
