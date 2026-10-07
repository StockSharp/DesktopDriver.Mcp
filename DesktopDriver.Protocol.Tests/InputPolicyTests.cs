namespace StockSharp.DesktopDriver.Tests.Protocol;

using System;
using System.Threading;
using System.Threading.Tasks;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using StockSharp.DesktopDriver.Client;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Host;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// Opening a local session does not permit input to a run with live external connections.
/// </summary>
[TestClass]
public class InputPolicyTests : BaseTestClass
{
	[TestMethod]
	[Timeout(60000)]
	public async Task ARunWithoutATestProfileAnswersTheSessionAndRefusesInputBeforeResolvingTheControl()
	{
		var startup = UiAutomationStartup.For(
			"tests.live-profile", "1.0", "fixture", hasLiveOutsideWorld: true, isTestProfile: false);
		var opened = new UiAutomationComposition().Open(startup, new UiToolkitParts(
			Mock.Of<IUiExecutor>(),
			Mock.Of<IUiRootSource>(),
			Mock.Of<IUiPresentationReader>(),
			Mock.Of<IUiInputTargetResolver>(),
			() => throw new InvalidOperationException("An unsafe run must not create an input backend."),
			null,
			"test"));
		await using var host = opened.Host;
		await using var client = await UiAutomationClient.ConnectAsync(
			opened.Endpoint, startup.AppId, TimeSpan.FromSeconds(10), CancellationToken.None);

		var session = await client.GetSessionAsync(CancellationToken.None);
		IsFalse(session.SafeTestProfile);
		IsNull(session.FixtureId);
		AreEqual("none", session.InputBackend);
		AreEqual(0, session.Capabilities.Length);

		var error = await ThrowsAsync<UiAutomationException>(() => client.ExecuteInputAsync(new UiInputRequest(
			Guid.NewGuid(),
			UiTarget.FromId(new UiNodeId("window:test", "unresolved")),
			UiControlPart.Instance,
			new UiClickAction(UiPointerButtons.Left, 1),
			null,
			TimeSpan.FromSeconds(5)), CancellationToken.None));

		AreEqual(UiErrorCodes.InputUnavailable, error.Error.Code);
	}
}
