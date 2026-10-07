namespace StockSharp.DesktopDriver.Client;

using System;
using System.Collections.Immutable;
using System.IO;
using System.IO.Pipes;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Api;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Protocol;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Serialization;
using StockSharp.DesktopDriver.Session;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Waiting;

/// <summary>
/// Talks to a running application over its local channel.
/// </summary>
/// <remarks>
/// It implements the same interface the service does and adds nothing: a client that computed anything
/// would be a second opinion about the interface, and the two would disagree eventually.
/// </remarks>
public sealed class UiAutomationClient : IUiAutomationClient
{
	private readonly SemaphoreSlim _oneAtATime = new(1, 1);
	private readonly NamedPipeClientStream _pipe;
	private readonly Guid _instanceId;
	private readonly string _protocolVersion;

	private UiAutomationClient(NamedPipeClientStream pipe, Guid instanceId, string protocolVersion)
	{
		_pipe = pipe;
		_instanceId = instanceId;
		_protocolVersion = protocolVersion;
	}

	/// <summary>
	/// What the session said when the connection opened.
	/// </summary>
	public UiSessionInfo Session { get; private set; }

	/// <summary>
	/// Connects to an application and opens a session.
	/// </summary>
	/// <param name="endpoint">Where it is listening.</param>
	/// <param name="expectedAppId">Which product the caller expects.</param>
	/// <param name="connectTimeout">How long to wait for the pipe.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The connected client.</returns>
	public static async Task<UiAutomationClient> ConnectAsync(
		UiEndpointInfo endpoint,
		string expectedAppId,
		TimeSpan connectTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(endpoint);

		var pipe = new NamedPipeClientStream(
			".",
			endpoint.PipeName,
			PipeDirection.InOut,
			PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

		await pipe.ConnectAsync((int)connectTimeout.TotalMilliseconds, cancellationToken).ConfigureAwait(false);

		var client = new UiAutomationClient(pipe, endpoint.InstanceId, endpoint.ProtocolVersion);

		client.Session = await client
			.CallAsync<UiSessionInfo>(
				UiMethods.SessionOpen,
				new UiSessionOpenParams(expectedAppId, endpoint.InstanceId, endpoint.ProtocolVersion),
				cancellationToken)
			.ConfigureAwait(false);

		return client;
	}

	/// <inheritdoc />
	public Task<UiSessionInfo> GetSessionAsync(CancellationToken cancellationToken)
		=> CallAsync<UiSessionInfo>(UiMethods.SessionInfo, null, cancellationToken);

	/// <inheritdoc />
	public Task<ImmutableArray<UiSurfaceInfo>> GetSurfacesAsync(CancellationToken cancellationToken)
		=> CallAsync<ImmutableArray<UiSurfaceInfo>>(UiMethods.Windows, null, cancellationToken);

	/// <inheritdoc />
	public Task<UiFindResult> FindAsync(UiFindQuery query, CancellationToken cancellationToken)
		=> CallAsync<UiFindResult>(UiMethods.Find, query, cancellationToken);

	/// <inheritdoc />
	public Task<UiTreeSnapshot> GetTreeAsync(UiTreeQuery query, CancellationToken cancellationToken)
		=> CallAsync<UiTreeSnapshot>(UiMethods.Tree, query, cancellationToken);

	/// <inheritdoc />
	public Task<UiNodeSnapshot> CaptureAsync(UiTarget target, UiCaptureOptions options, CancellationToken cancellationToken)
		=> CallAsync<UiNodeSnapshot>(UiMethods.Snapshot, new UiCaptureParams(target, options), cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<GridColumnSnapshot>> ReadGridColumnsAsync(
		UiTarget target, GridColumnsQuery query, CancellationToken cancellationToken)
		=> CallAsync<UiDataPage<GridColumnSnapshot>>(
			UiMethods.GridColumns, new UiGridColumnsParams(target, query), cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<GridRowSnapshot>> ReadGridRowsAsync(
		UiTarget target, GridRowsQuery query, CancellationToken cancellationToken)
		=> CallAsync<UiDataPage<GridRowSnapshot>>(
			UiMethods.GridRows, new UiGridRowsParams(target, query), cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<GridGroupSnapshot>> ReadGridGroupsAsync(
		UiTarget target, GridGroupsQuery query, CancellationToken cancellationToken)
		=> CallAsync<UiDataPage<GridGroupSnapshot>>(
			UiMethods.GridGroups, new UiGridGroupsParams(target, query), cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<ChartSeriesSnapshot>> ReadChartSeriesAsync(
		UiTarget target, ChartSeriesQuery query, CancellationToken cancellationToken)
		=> CallAsync<UiDataPage<ChartSeriesSnapshot>>(
			UiMethods.ChartSeries, new UiChartSeriesParams(target, query), cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<ChartPointSnapshot>> ReadChartPointsAsync(
		UiTarget target, ChartPointsQuery query, CancellationToken cancellationToken)
		=> CallAsync<UiDataPage<ChartPointSnapshot>>(
			UiMethods.ChartPoints, new UiChartPointsParams(target, query), cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<OrderBookLevelSnapshot>> ReadOrderBookLevelsAsync(
		UiTarget target, OrderBookLevelsQuery query, CancellationToken cancellationToken)
		=> CallAsync<UiDataPage<OrderBookLevelSnapshot>>(
			UiMethods.OrderBookLevels, new UiOrderBookLevelsParams(target, query), cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<PropertyItemSnapshot>> ReadPropertyEditorItemsAsync(
		UiTarget target, PropertyEditorItemsQuery query, CancellationToken cancellationToken)
		=> CallAsync<UiDataPage<PropertyItemSnapshot>>(
			UiMethods.PropertyEditorItems, new UiPropertyEditorItemsParams(target, query), cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<TreeItemSnapshot>> ReadTreeItemsAsync(
		UiTarget target, TreeItemsQuery query, CancellationToken cancellationToken)
		=> CallAsync<UiDataPage<TreeItemSnapshot>>(
			UiMethods.TreeItems, new UiTreeItemsParams(target, query), cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<DocumentLineSnapshot>> ReadDocumentContentAsync(
		UiTarget target, DocumentContentQuery query, CancellationToken cancellationToken)
		=> CallAsync<UiDataPage<DocumentLineSnapshot>>(
			UiMethods.DocumentContent, new UiDocumentContentParams(target, query), cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<DiagramNodeSnapshot>> ReadDiagramNodesAsync(
		UiTarget target, DiagramNodesQuery query, CancellationToken cancellationToken)
		=> CallAsync<UiDataPage<DiagramNodeSnapshot>>(
			UiMethods.DiagramNodes, new UiDiagramNodesParams(target, query), cancellationToken);

	/// <inheritdoc />
	public Task<UiDataPage<DiagramConnectionSnapshot>> ReadDiagramConnectionsAsync(
		UiTarget target, DiagramConnectionsQuery query, CancellationToken cancellationToken)
		=> CallAsync<UiDataPage<DiagramConnectionSnapshot>>(
			UiMethods.DiagramConnections, new UiDiagramConnectionsParams(target, query), cancellationToken);

	/// <inheritdoc />
	public Task<DockLayoutSnapshot> ReadDockLayoutAsync(
		UiTarget target, DockLayoutQuery query, CancellationToken cancellationToken)
		=> CallAsync<DockLayoutSnapshot>(
			UiMethods.DockLayout, new UiDockLayoutParams(target, query), cancellationToken);

	/// <inheritdoc />
	public Task<UiActionReceipt> ExecuteInputAsync(UiInputRequest request, CancellationToken cancellationToken)
		=> CallAsync<UiActionReceipt>(UiMethods.Input, request, cancellationToken);

	/// <inheritdoc />
	public Task<UiActionReceipt> GetActionStatusAsync(Guid actionId, CancellationToken cancellationToken)
		=> CallAsync<UiActionReceipt>(UiMethods.ActionStatus, new UiActionStatusParams(actionId), cancellationToken);

	/// <inheritdoc />
	public Task<UiWaitResult> WaitAsync(UiWaitRequest request, CancellationToken cancellationToken)
		=> CallAsync<UiWaitResult>(UiMethods.Wait, request, cancellationToken);

	/// <inheritdoc />
	public Task<UiScreenshotInfo> CaptureScreenshotAsync(UiScreenshotRequest request, CancellationToken cancellationToken)
		=> CallAsync<UiScreenshotInfo>(UiMethods.Screenshot, request, cancellationToken);

	/// <inheritdoc />
	public Task<UiArtifactChunk> ReadArtifactAsync(UiArtifactReadRequest request, CancellationToken cancellationToken)
		=> CallAsync<UiArtifactChunk>(UiMethods.ArtifactRead, request, cancellationToken);

	/// <inheritdoc />
	public Task<UiDiagnosticPage> ReadDiagnosticsAsync(UiDiagnosticQuery query, CancellationToken cancellationToken)
		=> CallAsync<UiDiagnosticPage>(UiMethods.Diagnostics, query, cancellationToken);

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await Task.CompletedTask.ConfigureAwait(false);

		_pipe.Dispose();
		_oneAtATime.Dispose();
	}

	private async Task<T> CallAsync<T>(string method, object parameters, CancellationToken cancellationToken)
	{
		await _oneAtATime.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			var requestId = Guid.NewGuid();
			var envelope = new UiRequestEnvelope(
				_protocolVersion,
				requestId,
				_instanceId,
				method,
				0,
				parameters is null ? JsonNode.Parse("{}") : JsonNode.Parse(UiJson.Write(parameters)));

			await UiPipeFramingBridge.WriteAsync(_pipe, UiJson.Write(envelope), cancellationToken).ConfigureAwait(false);

			var text = await UiPipeFramingBridge.ReadAsync(_pipe, cancellationToken).ConfigureAwait(false)
				?? throw new UiAutomationException(UiError.Create(
					UiErrorCodes.ApplicationExited,
					"The application closed the connection."));

			var response = UiJson.Read<UiResponseEnvelope>(text);

			if (response.Error is not null)
				throw new UiAutomationException(response.Error);

			if (response.RequestId != requestId)
			{
				throw new UiAutomationException(UiError.Create(
					UiErrorCodes.ProtocolMismatch,
					"The reply does not answer the request that was sent."));
			}

			return UiJson.Read<T>(response.Result.ToJsonString());
		}
		finally
		{
			_oneAtATime.Release();
		}
	}
}
