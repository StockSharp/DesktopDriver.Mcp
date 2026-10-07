namespace StockSharp.DesktopDriver.Protocol;

using System;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;

/// <summary>
/// Arguments of <see cref="UiMethods.Snapshot"/>.
/// </summary>
/// <param name="Target">Which node.</param>
/// <param name="Options">How much of it.</param>
public sealed record UiCaptureParams(UiTarget Target, UiCaptureOptions Options);

/// <summary>
/// Arguments of <see cref="UiMethods.GridColumns"/>.
/// </summary>
/// <param name="Target">Which grid.</param>
/// <param name="Query">Which columns.</param>
public sealed record UiGridColumnsParams(UiTarget Target, GridColumnsQuery Query);

/// <summary>
/// Arguments of <see cref="UiMethods.GridRows"/>.
/// </summary>
/// <param name="Target">Which grid.</param>
/// <param name="Query">Which rows.</param>
public sealed record UiGridRowsParams(UiTarget Target, GridRowsQuery Query);

/// <summary>
/// Arguments of <see cref="UiMethods.GridGroups"/>.
/// </summary>
/// <param name="Target">Which grid.</param>
/// <param name="Query">Which groups.</param>
public sealed record UiGridGroupsParams(UiTarget Target, GridGroupsQuery Query);

/// <summary>
/// Arguments of <see cref="UiMethods.ChartSeries"/>.
/// </summary>
/// <param name="Target">Which chart.</param>
/// <param name="Query">Which series.</param>
public sealed record UiChartSeriesParams(UiTarget Target, ChartSeriesQuery Query);

/// <summary>
/// Arguments of <see cref="UiMethods.ChartPoints"/>.
/// </summary>
/// <param name="Target">Which chart.</param>
/// <param name="Query">Which points.</param>
public sealed record UiChartPointsParams(UiTarget Target, ChartPointsQuery Query);

/// <summary>
/// Arguments of <see cref="UiMethods.OrderBookLevels"/>.
/// </summary>
/// <param name="Target">Which book.</param>
/// <param name="Query">Which levels.</param>
public sealed record UiOrderBookLevelsParams(UiTarget Target, OrderBookLevelsQuery Query);

/// <summary>
/// Arguments of <see cref="UiMethods.PropertyEditorItems"/>.
/// </summary>
/// <param name="Target">Which editor.</param>
/// <param name="Query">Which properties.</param>
public sealed record UiPropertyEditorItemsParams(UiTarget Target, PropertyEditorItemsQuery Query);

/// <summary>
/// Arguments of <see cref="UiMethods.TreeItems"/>.
/// </summary>
/// <param name="Target">Which tree.</param>
/// <param name="Query">Which items.</param>
public sealed record UiTreeItemsParams(UiTarget Target, TreeItemsQuery Query);

/// <summary>
/// Arguments of <see cref="UiMethods.DocumentContent"/>.
/// </summary>
/// <param name="Target">Which document.</param>
/// <param name="Query">Which lines.</param>
public sealed record UiDocumentContentParams(UiTarget Target, DocumentContentQuery Query);

/// <summary>
/// Arguments of <see cref="UiMethods.DiagramNodes"/>.
/// </summary>
/// <param name="Target">Which diagram.</param>
/// <param name="Query">Which blocks.</param>
public sealed record UiDiagramNodesParams(UiTarget Target, DiagramNodesQuery Query);

/// <summary>
/// Arguments of <see cref="UiMethods.DiagramConnections"/>.
/// </summary>
/// <param name="Target">Which diagram.</param>
/// <param name="Query">Which connections.</param>
public sealed record UiDiagramConnectionsParams(UiTarget Target, DiagramConnectionsQuery Query);

/// <summary>
/// Arguments of <see cref="UiMethods.DockLayout"/>.
/// </summary>
/// <param name="Target">Which workspace.</param>
/// <param name="Query">Which part of it.</param>
public sealed record UiDockLayoutParams(UiTarget Target, DockLayoutQuery Query);

/// <summary>
/// Arguments of <see cref="UiMethods.ActionStatus"/>.
/// </summary>
/// <param name="ActionId">The action.</param>
public sealed record UiActionStatusParams(Guid ActionId);

/// <summary>
/// Arguments of <see cref="UiMethods.RequestCancel"/>.
/// </summary>
/// <param name="RequestId">The request to abandon.</param>
public sealed record UiCancelParams(Guid RequestId);
