namespace StockSharp.DesktopDriver.Runner;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Client;
using StockSharp.DesktopDriver.Protocol;
using StockSharp.DesktopDriver.Serialization;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// Where an application is listening, and which product is expected to answer.
/// </summary>
/// <param name="Endpoint">What the application published about itself.</param>
/// <param name="ExpectedAppId">The product the caller means to reach.</param>
/// <param name="Patience">How long to wait for the connection.</param>
public sealed record UiConnection(
	UiEndpointInfo Endpoint,
	string ExpectedAppId,
	TimeSpan Patience)
{
	/// <summary>
	/// The variable naming the endpoint file, for a caller that would rather not repeat it.
	/// </summary>
	public const string EndpointVariable = "STOCKSHARP_UI_ENDPOINT";

	/// <summary>
	/// Reads what an application published about itself.
	/// </summary>
	/// <param name="endpointFile">The endpoint file it wrote.</param>
	/// <param name="expectedAppId">The product expected to answer, or <see langword="null"/> for whichever
	/// one the endpoint names.</param>
	/// <param name="patience">How long to wait for the connection.</param>
	/// <returns>The connection.</returns>
	/// <exception cref="UiUnreachableException">There is no such endpoint file.</exception>
	public static UiConnection To(
		string endpointFile,
		string expectedAppId,
		TimeSpan patience)
	{
		ArgumentException.ThrowIfNullOrEmpty(endpointFile);

		var full = Path.GetFullPath(endpointFile);

		if (!File.Exists(full))
		{
			throw new UiUnreachableException(
				$"There is no endpoint file at {full}. An application writes one once it is drawn, and only " +
				$"when it was started with {UiLaunchProtocol.EnableSwitch}.");
		}

		// Whatever scans new files on the machine can hold this one for a moment after it appears.
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
		UiEndpointInfo endpoint;

		while (!UiEndpointFile.TryRead(full, out endpoint))
		{
			if (DateTime.UtcNow >= deadline)
				throw new UiUnreachableException($"The endpoint file at {full} could not be read.");

			Thread.Sleep(50);
		}

		return new UiConnection(endpoint, expectedAppId ?? endpoint.AppId, patience);
	}

	/// <summary>
	/// Opens it.
	/// </summary>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The connected client.</returns>
	public Task<UiAutomationClient> OpenAsync(CancellationToken cancellationToken)
		=> UiAutomationClient.ConnectAsync(Endpoint, ExpectedAppId, Patience, cancellationToken);
}
