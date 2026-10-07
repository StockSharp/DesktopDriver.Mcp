namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Linq;
using System.Text.Json;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Protocol;
using StockSharp.DesktopDriver.Serialization;

/// <summary>
/// The session handshake carries only the expected product, instance and protocol.
/// </summary>
[TestClass]
public class SessionProtocolTests : BaseTestClass
{
	[TestMethod]
	public void TheHandshakeHasOnlyThreeFieldsAndRoundTrips()
	{
		var parameters = new UiSessionOpenParams("stocksharp.test", Guid.NewGuid(), UiProtocol.Version);
		var json = UiJson.Write(parameters);
		using var document = JsonDocument.Parse(json);
		var fields = document.RootElement;

		AreEqual(3, fields.EnumerateObject().Count());
		AreEqual(parameters.ExpectedAppId, fields.GetProperty("expectedAppId").GetString());
		AreEqual(parameters.ExpectedInstanceId, fields.GetProperty("expectedInstanceId").GetGuid());
		AreEqual(parameters.ProtocolVersion, fields.GetProperty("protocolVersion").GetString());
		AreEqual(parameters, UiJson.Read<UiSessionOpenParams>(json));
	}
}
