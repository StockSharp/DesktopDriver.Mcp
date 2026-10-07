namespace StockSharp.DesktopDriver.Tests.Protocol;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Host;

/// <summary>
/// Automation is enabled explicitly and needs only an optional endpoint file.
/// </summary>
[TestClass]
public class LaunchTests : BaseTestClass
{
	[TestMethod]
	public void AnOrdinaryLaunchDoesNotEnableAutomation()
	{
		IsNull(UiAutomationLaunch.TryRead(null));
		IsNull(UiAutomationLaunch.TryRead([]));
		IsNull(UiAutomationLaunch.TryRead(["--ui-automation-endpoint=endpoint.json"]));
		IsNull(UiAutomationLaunch.TryRead(["--ui-automation=true"]));
	}

	[TestMethod]
	public void TheEnableSwitchIsEnoughToStartAutomation()
	{
		var launch = UiAutomationLaunch.TryRead(["--ui-automation"]);

		IsNotNull(launch);
		IsNull(launch.EndpointFile);
	}

	[TestMethod]
	public void TheEndpointFileIsReadWhenAutomationIsEnabled()
	{
		var launch = UiAutomationLaunch.TryRead(["--ui-automation", "--ui-automation-endpoint=folder with spaces/endpoint.json"]);

		IsNotNull(launch);
		AreEqual("folder with spaces/endpoint.json", launch.EndpointFile);
	}

	[TestMethod]
	public void OnlyAutomationSwitchesAreStripped()
	{
		var remaining = UiAutomationLaunch.Strip(["--ui-test", "--ui-automation", "--ui-automation-endpoint=endpoint.json", "--route=grid"]);

		AreEqual(2, remaining.Length);
		AreEqual("--ui-test", remaining[0]);
		AreEqual("--route=grid", remaining[1]);
		AreEqual(0, UiAutomationLaunch.Strip(null).Length);
	}
}
