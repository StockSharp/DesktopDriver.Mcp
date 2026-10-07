namespace StockSharp.DesktopDriver.Host;

using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Api;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Protocol;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Serialization;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// The local endpoint an application opens when it is started for automation.
/// </summary>
/// <remarks>
/// A pipe restricted to the current user and a session handshake that checks the expected product,
/// instance and protocol before operations are accepted. The input policy decides whether the run may
/// be driven.
/// <para>
/// The host holds no logic of its own. It checks the session, parses, calls the same API an in-process test
/// calls, and writes the answer back.
/// </para>
/// </remarks>
public sealed class UiAutomationHost(IUiAutomationApi api, UiSessionInfo session) : IUiAutomationHost
{
	private readonly IUiAutomationApi _api = api ?? throw new ArgumentNullException(nameof(api));
	private readonly UiSessionInfo _session = session ?? throw new ArgumentNullException(nameof(session));
	private readonly UiRequestDispatcher _dispatcher = new(api);
	private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _running = new();

	private CancellationTokenSource _stopping;
	private Task _accepting;
	private string _pipeName;

	/// <summary>
	/// Where this host is listening, once it has started.
	/// </summary>
	public UiEndpointInfo Endpoint { get; private set; }

	/// <inheritdoc />
	public Task<UiEndpointInfo> StartAsync(UiHostOptions options, CancellationToken cancellationToken)
		=> Task.FromResult(Start(options));

	/// <summary>
	/// Opens the endpoint.
	/// </summary>
	/// <param name="options">How it is opened.</param>
	/// <returns>Where it is listening.</returns>
	/// <remarks>
	/// Nothing here waits, so an application can open its endpoint on the way up without an asynchronous
	/// startup path of its own. Connections are accepted afterwards, away from the interface's thread.
	/// </remarks>
	public UiEndpointInfo Start(UiHostOptions options)
	{
		if (_accepting is not null)
			throw new InvalidOperationException("This host is already listening.");

		_pipeName = $"stocksharp.ui.{_session.AppId}.{_session.InstanceId:N}";
		_stopping = new CancellationTokenSource();

		Endpoint = new UiEndpointInfo(
			_session.AppId,
			_session.InstanceId,
			_pipeName,
			_session.ProtocolVersion,
			Environment.ProcessId);

		_accepting = Task.Run(() => AcceptAsync(_stopping.Token), CancellationToken.None);

		return Endpoint;
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		var stopping = Interlocked.Exchange(ref _stopping, null);

		if (stopping is null)
			return;

		await stopping.CancelAsync().ConfigureAwait(false);

		var accepting = Interlocked.Exchange(ref _accepting, null);

		if (accepting is not null)
		{
			try
			{
				await accepting.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
			}
			catch (Exception)
			{
				// Shutting down is not allowed to fail: whatever is still in flight is abandoned.
			}
		}

		stopping.Dispose();
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None).ConfigureAwait(false);

	private async Task AcceptAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			NamedPipeServerStream pipe = null;

			try
			{
				pipe = new NamedPipeServerStream(
					_pipeName,
					PipeDirection.InOut,
					NamedPipeServerStream.MaxAllowedServerInstances,
					PipeTransmissionMode.Byte,
					PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

				await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

				var connection = pipe;
				pipe = null;

				_ = Task.Run(() => ServeAsync(connection, cancellationToken), CancellationToken.None);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception)
			{
				// One failed accept must not end the endpoint for everyone else.
			}
			finally
			{
				pipe?.Dispose();
			}
		}
	}

	private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
	{
		using (pipe)
		{
			var sessionOpened = false;

			try
			{
				while (!cancellationToken.IsCancellationRequested && pipe.IsConnected)
				{
					var text = await UiPipeFraming.ReadAsync(pipe, cancellationToken).ConfigureAwait(false);

					if (text is null)
						break;

					var request = UiJson.Read<UiRequestEnvelope>(text);
					var response = await HandleAsync(request, sessionOpened, cancellationToken).ConfigureAwait(false);

					if (request.Method == UiMethods.SessionOpen && response.Error is null)
						sessionOpened = true;

					await UiPipeFraming
						.WriteAsync(pipe, Within(request, response), cancellationToken)
						.ConfigureAwait(false);
				}
			}
			catch (Exception)
			{
				// A broken connection is the client's problem; the endpoint stays open for the next one.
			}
		}
	}

	private async Task<UiResponseEnvelope> HandleAsync(
		UiRequestEnvelope request,
		bool sessionOpened,
		CancellationToken cancellationToken)
	{
		try
		{
			if (request.Method == UiMethods.SessionOpen)
				return Ok(request, JsonNode.Parse(UiJson.Write(Open(request))));

			if (!sessionOpened)
			{
				return Fail(request, UiError.Create(
					UiErrorCodes.Unauthorized,
					"This connection has not opened a session."));
			}

			if (request.InstanceId != _session.InstanceId)
			{
				return Fail(request, UiError.Create(
					UiErrorCodes.NotFound,
					$"This is {_session.AppId} {_session.InstanceId}, not {request.InstanceId}."));
			}

			if (request.Method == UiMethods.RequestCancel)
			{
				var parameters = UiJson.Read<UiCancelParams>(request.Parameters.ToJsonString());

				if (_running.TryGetValue(parameters.RequestId, out var running))
					await running.CancelAsync().ConfigureAwait(false);

				return Ok(request, JsonNode.Parse("{}"));
			}

			using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

			if (request.TimeoutMs > 0)
				deadline.CancelAfter(request.TimeoutMs);

			_running[request.RequestId] = deadline;

			try
			{
				return Ok(request, await _dispatcher.DispatchAsync(request, deadline.Token).ConfigureAwait(false));
			}
			finally
			{
				_running.TryRemove(request.RequestId, out _);
			}
		}
		catch (UiAutomationException error)
		{
			return Fail(request, error.Error);
		}
		catch (OperationCanceledException)
		{
			return Fail(request, UiError.Create(UiErrorCodes.Cancelled, "The request was abandoned."));
		}
		catch (Exception error)
		{
			return Fail(request, UiError.Create(UiErrorCodes.UiUnavailable, error.Message));
		}
	}

	private UiSessionInfo Open(UiRequestEnvelope request)
	{
		var parameters = UiJson.Read<UiSessionOpenParams>(request.Parameters?.ToJsonString() ?? "{}");

		if (Major(parameters.ProtocolVersion) != Major(_session.ProtocolVersion))
		{
			throw new UiAutomationException(UiError.Create(
				UiErrorCodes.ProtocolMismatch,
				$"This host speaks {_session.ProtocolVersion}."));
		}

		if (!string.IsNullOrEmpty(parameters.ExpectedAppId) && parameters.ExpectedAppId != _session.AppId)
		{
			throw new UiAutomationException(UiError.Create(
				UiErrorCodes.NotFound,
				$"This is {_session.AppId}, not {parameters.ExpectedAppId}."));
		}

		if (parameters.ExpectedInstanceId != Guid.Empty && parameters.ExpectedInstanceId != _session.InstanceId)
		{
			throw new UiAutomationException(UiError.Create(
				UiErrorCodes.NotFound,
				"That is a different running copy of this product."));
		}

		return _session;
	}

	// A reply that does not fit is refused with a reason, not written until the framing throws and the
	// connection dies: a caller that lost the pipe has no way to tell an oversized answer from a crash,
	// and would retry the same read forever. The limit is the session's, capped by what the wire carries.
	private string Within(UiRequestEnvelope request, UiResponseEnvelope response)
	{
		var json = UiJson.Write(response);

		// The handshake is not a read, and it is where the caller learns what the limits are. Refusing it
		// for being too large would leave nobody able to find out why.
		if (request.Method == UiMethods.SessionOpen)
			return json;

		var size = Encoding.UTF8.GetByteCount(json);
		var limit = Math.Min(
			(_session.Budget ?? UiReadBudget.Default).MaxBytes,
			UiPipeFraming.MaxMessageBytes);

		if (size <= limit)
			return json;

		return UiJson.Write(Fail(request, UiError.Create(
			UiErrorCodes.LimitExceeded,
			$"The answer to '{request.Method}' is {size} bytes and this session carries {limit}. " +
			"Ask for less of it: a smaller page, fewer columns, or a narrower subtree.")));
	}

	private UiResponseEnvelope Ok(UiRequestEnvelope request, JsonNode result)
		=> new(_session.ProtocolVersion, request.RequestId, result, null);

	private UiResponseEnvelope Fail(UiRequestEnvelope request, UiError error)
		=> new(_session.ProtocolVersion, request.RequestId, null, error);

	private static string Major(string version)
		=> string.IsNullOrEmpty(version) ? string.Empty : version.Split('.')[0];
}
