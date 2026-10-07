namespace StockSharp.DesktopDriver.Tests.Cli;

using System;
using System.IO;

using Ecng.IO;
using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Cli;
using StockSharp.DesktopDriver.Runner;
using StockSharp.DesktopDriver.Serialization;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// The file a product writes once its endpoint is open.
/// </summary>
/// <remarks>
/// It is moved into place whole, so it is never read half-written; but whatever scans new files on the machine
/// can hold it for a moment after it appears, and a caller that gives up on that moment starts the product
/// again for nothing.
/// </remarks>
[TestClass]
public class EndpointFileTests : BaseTestClass
{
	private string _folder;

	[TestInitialize]
	public void CreateFolder() => _folder = LocalFileSystem.Instance.CreateTempDir();

	[TestCleanup]
	public void DeleteFolder() => Directory.Delete(_folder, true);

	[TestMethod]
	public void AFileSomebodyElseHoldsIsReadOnceItIsLetGo()
	{
		var path = Path.Combine(_folder, "endpoint.json");
		var written = new UiEndpointInfo("stocksharp.test", Guid.NewGuid(), "pipe", "1.0", 42);

		File.WriteAllText(path, UiJson.Write(written));

		using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
			IsFalse(UiEndpointFile.TryRead(path, out _));

		IsTrue(UiEndpointFile.TryRead(path, out var read));
		AreEqual(written, read);
	}

	[TestMethod]
	public void AFileThatIsNotThereYetIsNotRead()
		=> IsFalse(UiEndpointFile.TryRead(Path.Combine(_folder, "endpoint.json"), out _));

	[TestMethod]
	public void AConnectionNeedsOnlyTheEndpointFile()
	{
		var path = Path.Combine(_folder, "endpoint.json");
		var endpoint = new UiEndpointInfo("stocksharp.test", Guid.NewGuid(), "pipe", "1.0", 42);
		File.WriteAllText(path, UiJson.Write(endpoint));

		var arguments = UiArguments.Parse(["--endpoint", path, "--connect-timeout=3"]);
		var connection = UiConnectionArguments.Read(arguments);
		arguments.RefuseUnknown();

		AreEqual(endpoint, connection.Endpoint);
		AreEqual(endpoint.AppId, connection.ExpectedAppId);
		AreEqual(TimeSpan.FromSeconds(3), connection.Patience);
	}
}
