namespace StockSharp.DesktopDriver.Tests.Protocol;

using System;
using System.Threading;
using System.Threading.Tasks;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Protocol;
using StockSharp.DesktopDriver.Queries;

/// <summary>
/// What the endpoint does with an answer that is larger than the session allows.
/// </summary>
/// <remarks>
/// The wire carries a bounded message, so an answer that does not fit has to become something. Letting
/// the write fail turns it into a dropped connection, and a caller that lost the pipe cannot tell an
/// oversized answer from a crashed application - so it retries the same read and loses the pipe again.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class ResponseBudgetTests : BaseTestClass
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
	public Task AnAnswerLargerThanTheBudgetIsRefusedWithAReason() => RunAsync(async () =>
	{
		await using var hosted = await HostedApplication.StartAsync(
			UiReadBudget.Default with { MaxBytes = 256 },
			CancellationToken.None);

		await using var raw = await UiRawConnection.OpenAsync(hosted.Host.Endpoint, CancellationToken.None);

		IsNull((await raw.OpenSessionAsync(CancellationToken.None)).Error);

		var response = await raw.CallAsync(
			UiMethods.Windows,
			null,
			hosted.Session.InstanceId,
			hosted.Session.ProtocolVersion,
			CancellationToken.None);

		IsNotNull(response.Error, "An answer over the budget came back as though it fitted.");
		AreEqual(UiErrorCodes.LimitExceeded, response.Error.Code);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task TheConnectionSurvivesAnAnswerThatDidNotFit() => RunAsync(async () =>
	{
		await using var hosted = await HostedApplication.StartAsync(
			UiReadBudget.Default with { MaxBytes = 256 },
			CancellationToken.None);

		await using var raw = await UiRawConnection.OpenAsync(hosted.Host.Endpoint, CancellationToken.None);

		IsNull((await raw.OpenSessionAsync(CancellationToken.None)).Error);

		await raw.CallAsync(
			UiMethods.Windows,
			null,
			hosted.Session.InstanceId,
			hosted.Session.ProtocolVersion,
			CancellationToken.None);

		// The next request is the whole point: the caller has to be able to narrow the read and ask again
		// on the same connection, rather than reconnect and guess at what went wrong.
		var next = await raw.CallAsync(
			UiMethods.RequestCancel,
			new UiCancelParams(Guid.NewGuid()),
			hosted.Session.InstanceId,
			hosted.Session.ProtocolVersion,
			CancellationToken.None);

		IsNull(next.Error);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AnOrdinaryAnswerIsNotTouched() => RunAsync(async () =>
	{
		await using var hosted = await HostedApplication.StartAsync(CancellationToken.None);
		await using var raw = await UiRawConnection.OpenAsync(hosted.Host.Endpoint, CancellationToken.None);

		IsNull((await raw.OpenSessionAsync(CancellationToken.None)).Error);

		var response = await raw.CallAsync(
			UiMethods.Windows,
			null,
			hosted.Session.InstanceId,
			hosted.Session.ProtocolVersion,
			CancellationToken.None);

		IsNull(response.Error);
	});
}
