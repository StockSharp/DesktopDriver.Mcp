namespace StockSharp.DesktopDriver.Tests.Protocol;

using System;
using System.Threading;
using System.Threading.Tasks;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Client;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Protocol;

/// <summary>
/// What the endpoint accepts before and after the session handshake.
/// </summary>
/// <remarks>
/// The local channel checks the product, running copy and protocol before accepting operations.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class HandshakeTests : BaseTestClass
{
	// The body is handed over as a lambda the session can see is asynchronous, not as a delegate: given a
	// delegate the session runs it, takes back the task it returned and never waits on it, so the
	// interface thread stops pumping the moment the body first awaits and a failed assertion is never
	// seen at all.
	private static Task RunAsync(Func<Task> body)
		=> AssemblyInitializer.Session.Dispatch(async () =>
		{
			await body();
			return true;
		}, CancellationToken.None);

	[TestMethod]
	[Timeout(60000)]
	public Task NothingIsAnsweredBeforeASessionIsOpened() => RunAsync(async () =>
	{
		await using var hosted = await HostedApplication.StartAsync(CancellationToken.None);
		await using var raw = await UiRawConnection.OpenAsync(hosted.Host.Endpoint, CancellationToken.None);

		var response = await raw.CallAsync(
			UiMethods.Windows,
			null,
			hosted.Session.InstanceId,
			hosted.Session.ProtocolVersion,
			CancellationToken.None);

		IsNotNull(response.Error, "The endpoint listed its windows to a caller that had not opened a session.");
		AreEqual(UiErrorCodes.Unauthorized, response.Error.Code);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task ASessionOpensWithOnlyProductInstanceAndProtocol() => RunAsync(async () =>
	{
		await using var hosted = await HostedApplication.StartAsync(CancellationToken.None);
		await using var raw = await UiRawConnection.OpenAsync(hosted.Host.Endpoint, CancellationToken.None);

		var response = await raw.CallAsync(
			UiMethods.SessionOpen,
			new
			{
				ExpectedAppId = HostedApplication.AppId,
				ExpectedInstanceId = hosted.Session.InstanceId,
				ProtocolVersion = hosted.Session.ProtocolVersion,
			},
			hosted.Session.InstanceId,
			hosted.Session.ProtocolVersion,
			CancellationToken.None);

		IsNull(response.Error);

		response = await raw.CallAsync(
			UiMethods.Windows,
			null,
			hosted.Session.InstanceId,
			hosted.Session.ProtocolVersion,
			CancellationToken.None);

		IsNull(response.Error);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task ARefusedHandshakeLeavesTheConnectionUnopened() => RunAsync(async () =>
	{
		// Refusing the handshake and then answering the next request anyway would make the handshake
		// decoration. The connection has to stay shut.
		await using var hosted = await HostedApplication.StartAsync(CancellationToken.None);
		await using var raw = await UiRawConnection.OpenAsync(hosted.Host.Endpoint, CancellationToken.None);

		var refused = await raw.CallAsync(
			UiMethods.SessionOpen,
			new UiSessionOpenParams("some.other.product", hosted.Session.InstanceId, hosted.Session.ProtocolVersion),
			hosted.Session.InstanceId,
			hosted.Session.ProtocolVersion,
			CancellationToken.None);

		IsNotNull(refused.Error);
		AreEqual(UiErrorCodes.NotFound, refused.Error.Code);

		var response = await raw.CallAsync(
			UiMethods.Windows,
			null,
			hosted.Session.InstanceId,
			hosted.Session.ProtocolVersion,
			CancellationToken.None);

		IsNotNull(response.Error);
		AreEqual(UiErrorCodes.Unauthorized, response.Error.Code);

		IsNull((await raw.OpenSessionAsync(CancellationToken.None)).Error);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task ReachingTheWrongProductIsRefused() => RunAsync(async () =>
	{
		// Two products are running as often as not, and input sent to the wrong one is a click somebody
		// sees.
		await using var hosted = await HostedApplication.StartAsync(CancellationToken.None);

		var error = await ThrowsAsync<UiAutomationException>(
			() => hosted.ConnectAsync("some.other.product", CancellationToken.None));

		AreEqual(UiErrorCodes.NotFound, error.Error.Code);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AnotherVersionOfTheProtocolIsTurnedAwayAtTheHandshake() => RunAsync(async () =>
	{
		await using var hosted = await HostedApplication.StartAsync(CancellationToken.None);
		await using var raw = await UiRawConnection.OpenAsync(hosted.Host.Endpoint, CancellationToken.None);

		var response = await raw.CallAsync(
			UiMethods.SessionOpen,
			new UiSessionOpenParams(HostedApplication.AppId, hosted.Session.InstanceId, "2.0"),
			hosted.Session.InstanceId,
			"2.0",
			CancellationToken.None);

		IsNotNull(response.Error);
		AreEqual(UiErrorCodes.ProtocolMismatch, response.Error.Code);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task ARequestMeantForAnotherRunningCopyIsRefused() => RunAsync(async () =>
	{
		// The instance is named in every request, not assumed from the connection: a runner that drove
		// whatever answered would be driving whichever copy happened to start last.
		await using var hosted = await HostedApplication.StartAsync(CancellationToken.None);
		await using var raw = await UiRawConnection.OpenAsync(hosted.Host.Endpoint, CancellationToken.None);

		IsNull((await raw.OpenSessionAsync(CancellationToken.None)).Error);

		var response = await raw.CallAsync(
			UiMethods.Windows,
			null,
			Guid.NewGuid(),
			hosted.Session.ProtocolVersion,
			CancellationToken.None);

		IsNotNull(response.Error);
		AreEqual(UiErrorCodes.NotFound, response.Error.Code);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AnOperationThisProtocolDoesNotHaveIsRefused() => RunAsync(async () =>
	{
		// The method name is looked up in a closed list. A host that turned a string into a call by
		// reflection would let whoever can reach the channel run anything the process can.
		await using var hosted = await HostedApplication.StartAsync(CancellationToken.None);
		await using var raw = await UiRawConnection.OpenAsync(hosted.Host.Endpoint, CancellationToken.None);

		IsNull((await raw.OpenSessionAsync(CancellationToken.None)).Error);

		var response = await raw.CallAsync(
			"process.exit",
			null,
			hosted.Session.InstanceId,
			hosted.Session.ProtocolVersion,
			CancellationToken.None);

		IsNotNull(response.Error);
		AreEqual(UiErrorCodes.InvalidRequest, response.Error.Code);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AnotherRunningCopyIsRefusedAtTheHandshake() => RunAsync(async () =>
	{
		await using var hosted = await HostedApplication.StartAsync(CancellationToken.None);
		var endpoint = hosted.Host.Endpoint with { InstanceId = Guid.NewGuid() };

		var error = await ThrowsAsync<UiAutomationException>(() => UiAutomationClient.ConnectAsync(
			endpoint, HostedApplication.AppId, TimeSpan.FromSeconds(10), CancellationToken.None));

		AreEqual(UiErrorCodes.NotFound, error.Error.Code);
	});

}
