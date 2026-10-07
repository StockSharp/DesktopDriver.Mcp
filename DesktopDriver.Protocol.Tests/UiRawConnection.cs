namespace StockSharp.DesktopDriver.Tests.Protocol;

using System;
using System.IO.Pipes;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Client;
using StockSharp.DesktopDriver.Protocol;
using StockSharp.DesktopDriver.Serialization;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// The wire with no client on top of it.
/// </summary>
/// <remarks>
/// A client cannot send a request before opening a session or name an unknown operation - it only ever
/// names operations that exist. Showing what the endpoint does with the requests a client would never
/// send means writing them by hand.
/// </remarks>
internal sealed class UiRawConnection : IAsyncDisposable
{
	private readonly NamedPipeClientStream _pipe;
	private readonly UiEndpointInfo _endpoint;

	private UiRawConnection(NamedPipeClientStream pipe, UiEndpointInfo endpoint)
	{
		_pipe = pipe;
		_endpoint = endpoint;
	}

	/// <summary>
	/// Dials the endpoint without opening a session.
	/// </summary>
	/// <param name="endpoint">Where it is listening.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The open connection.</returns>
	public static async Task<UiRawConnection> OpenAsync(UiEndpointInfo endpoint, CancellationToken cancellationToken)
	{
		var pipe = new NamedPipeClientStream(
			".",
			endpoint.PipeName,
			PipeDirection.InOut,
			PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

		await pipe.ConnectAsync(10000, cancellationToken);

		return new UiRawConnection(pipe, endpoint);
	}

	/// <summary>
	/// Sends one request exactly as written and returns the reply.
	/// </summary>
	/// <param name="method">The operation name.</param>
	/// <param name="parameters">Its arguments.</param>
	/// <param name="instanceId">The running copy the request names.</param>
	/// <param name="protocolVersion">The version the caller claims.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The reply.</returns>
	public async Task<UiResponseEnvelope> CallAsync(
		string method,
		object parameters,
		Guid instanceId,
		string protocolVersion,
		CancellationToken cancellationToken)
	{
		var envelope = new UiRequestEnvelope(
			protocolVersion,
			Guid.NewGuid(),
			instanceId,
			method,
			0,
			parameters is null ? JsonNode.Parse("{}") : JsonNode.Parse(UiJson.Write(parameters)));

		await UiPipeFramingBridge.WriteAsync(_pipe, UiJson.Write(envelope), cancellationToken);

		var text = await UiPipeFramingBridge.ReadAsync(_pipe, cancellationToken)
			?? throw new InvalidOperationException("The endpoint closed the connection without answering.");

		return UiJson.Read<UiResponseEnvelope>(text);
	}

	/// <summary>
	/// Opens a session the ordinary way, so that what follows is about something other than the handshake.
	/// </summary>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The reply to the handshake.</returns>
	public Task<UiResponseEnvelope> OpenSessionAsync(CancellationToken cancellationToken)
		=> CallAsync(
			UiMethods.SessionOpen,
			new UiSessionOpenParams(_endpoint.AppId, _endpoint.InstanceId, _endpoint.ProtocolVersion),
			_endpoint.InstanceId,
			_endpoint.ProtocolVersion,
			cancellationToken);

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		_pipe.Dispose();

		return ValueTask.CompletedTask;
	}
}
