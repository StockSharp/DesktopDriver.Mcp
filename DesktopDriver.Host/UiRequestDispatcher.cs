namespace StockSharp.DesktopDriver.Host;

using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Api;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Protocol;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Serialization;

/// <summary>
/// Turns a method name into a call on the one implementation.
/// </summary>
/// <remarks>
/// A closed switch rather than a lookup by reflection. The host holds no logic of its own: every branch
/// here parses arguments and calls the same API an in-process test calls, so the two cannot drift.
/// </remarks>
public sealed class UiRequestDispatcher(IUiAutomationApi api)
{
	private readonly IUiAutomationApi _api = api ?? throw new ArgumentNullException(nameof(api));

	/// <summary>
	/// Runs one request.
	/// </summary>
	/// <param name="request">What was asked.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>What to send back.</returns>
	public async Task<JsonNode> DispatchAsync(UiRequestEnvelope request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		switch (request.Method)
		{
			case UiMethods.SessionInfo:
				return Write(await _api.GetSessionAsync(cancellationToken).ConfigureAwait(false));

			case UiMethods.Windows:
				return Write(await _api.GetSurfacesAsync(cancellationToken).ConfigureAwait(false));

			case UiMethods.Find:
				return Write(await _api.FindAsync(Read<UiFindQuery>(request), cancellationToken).ConfigureAwait(false));

			case UiMethods.Tree:
				return Write(await _api.GetTreeAsync(Read<UiTreeQuery>(request), cancellationToken).ConfigureAwait(false));

			case UiMethods.Snapshot:
			{
				var parameters = Read<UiCaptureParams>(request);

				return Write(await _api
					.CaptureAsync(parameters.Target, parameters.Options, cancellationToken)
					.ConfigureAwait(false));
			}

			case UiMethods.GridColumns:
			{
				var parameters = Read<UiGridColumnsParams>(request);

				return Write(await _api
					.ReadGridColumnsAsync(parameters.Target, parameters.Query, cancellationToken)
					.ConfigureAwait(false));
			}

			case UiMethods.GridRows:
			{
				var parameters = Read<UiGridRowsParams>(request);

				return Write(await _api
					.ReadGridRowsAsync(parameters.Target, parameters.Query, cancellationToken)
					.ConfigureAwait(false));
			}

			case UiMethods.GridGroups:
			{
				var parameters = Read<UiGridGroupsParams>(request);

				return Write(await _api
					.ReadGridGroupsAsync(parameters.Target, parameters.Query, cancellationToken)
					.ConfigureAwait(false));
			}

			case UiMethods.ChartSeries:
			{
				var parameters = Read<UiChartSeriesParams>(request);

				return Write(await _api
					.ReadChartSeriesAsync(parameters.Target, parameters.Query, cancellationToken)
					.ConfigureAwait(false));
			}

			case UiMethods.ChartPoints:
			{
				var parameters = Read<UiChartPointsParams>(request);

				return Write(await _api
					.ReadChartPointsAsync(parameters.Target, parameters.Query, cancellationToken)
					.ConfigureAwait(false));
			}

			case UiMethods.OrderBookLevels:
			{
				var parameters = Read<UiOrderBookLevelsParams>(request);

				return Write(await _api
					.ReadOrderBookLevelsAsync(parameters.Target, parameters.Query, cancellationToken)
					.ConfigureAwait(false));
			}

			case UiMethods.PropertyEditorItems:
			{
				var parameters = Read<UiPropertyEditorItemsParams>(request);

				return Write(await _api
					.ReadPropertyEditorItemsAsync(parameters.Target, parameters.Query, cancellationToken)
					.ConfigureAwait(false));
			}

			case UiMethods.TreeItems:
			{
				var parameters = Read<UiTreeItemsParams>(request);

				return Write(await _api
					.ReadTreeItemsAsync(parameters.Target, parameters.Query, cancellationToken)
					.ConfigureAwait(false));
			}

			case UiMethods.DocumentContent:
			{
				var parameters = Read<UiDocumentContentParams>(request);

				return Write(await _api
					.ReadDocumentContentAsync(parameters.Target, parameters.Query, cancellationToken)
					.ConfigureAwait(false));
			}

			case UiMethods.DiagramNodes:
			{
				var parameters = Read<UiDiagramNodesParams>(request);

				return Write(await _api
					.ReadDiagramNodesAsync(parameters.Target, parameters.Query, cancellationToken)
					.ConfigureAwait(false));
			}

			case UiMethods.DiagramConnections:
			{
				var parameters = Read<UiDiagramConnectionsParams>(request);

				return Write(await _api
					.ReadDiagramConnectionsAsync(parameters.Target, parameters.Query, cancellationToken)
					.ConfigureAwait(false));
			}

			case UiMethods.DockLayout:
			{
				var parameters = Read<UiDockLayoutParams>(request);

				return Write(await _api
					.ReadDockLayoutAsync(parameters.Target, parameters.Query, cancellationToken)
					.ConfigureAwait(false));
			}

			case UiMethods.Input:
				return Write(await _api
					.ExecuteInputAsync(Read<Input.UiInputRequest>(request), cancellationToken)
					.ConfigureAwait(false));

			case UiMethods.ActionStatus:
				return Write(await _api
					.GetActionStatusAsync(Read<UiActionStatusParams>(request).ActionId, cancellationToken)
					.ConfigureAwait(false));

			case UiMethods.Wait:
				return Write(await _api
					.WaitAsync(Read<Waiting.UiWaitRequest>(request), cancellationToken)
					.ConfigureAwait(false));

			case UiMethods.Screenshot:
				return Write(await _api
					.CaptureScreenshotAsync(Read<UiScreenshotRequest>(request), cancellationToken)
					.ConfigureAwait(false));

			case UiMethods.ArtifactRead:
				return Write(await _api
					.ReadArtifactAsync(Read<UiArtifactReadRequest>(request), cancellationToken)
					.ConfigureAwait(false));

			case UiMethods.Diagnostics:
				return Write(await _api
					.ReadDiagnosticsAsync(Read<UiDiagnosticQuery>(request), cancellationToken)
					.ConfigureAwait(false));

			default:
				throw new UiAutomationException(UiError.Create(
					UiErrorCodes.InvalidRequest,
					$"'{request.Method}' is not an operation of this protocol."));
		}
	}

	private static T Read<T>(UiRequestEnvelope request)
	{
		if (request.Parameters is null)
		{
			throw new UiAutomationException(UiError.Create(
				UiErrorCodes.InvalidRequest,
				$"'{request.Method}' needs arguments."));
		}

		try
		{
			return UiJson.Read<T>(request.Parameters.ToJsonString());
		}
		catch (Exception error) when (error is not UiAutomationException)
		{
			throw new UiAutomationException(
				UiError.Create(UiErrorCodes.InvalidRequest, $"The arguments of '{request.Method}' are not valid."),
				error);
		}
	}

	private static JsonNode Write<T>(T value) => JsonNode.Parse(UiJson.Write(value));
}
