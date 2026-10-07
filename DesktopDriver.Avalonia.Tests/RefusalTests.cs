namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Ecng.UnitTesting;

using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Session;
using StockSharp.DesktopDriver.States;

/// <summary>
/// What a control says when it is asked something its kind cannot answer.
/// </summary>
/// <remarks>
/// Every reading belongs to a kind: rows to a table, series to a chart, levels to an order book, items
/// to a tree. Asked of something else, the protocol refuses with one code, and a caller decides what to
/// do from that code rather than from a message somebody might reword.
/// <para>
/// The alternative is the one thing that must not happen: a button answering "no rows" and a test
/// passing because a table it never found is empty.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class RefusalTests : BaseTestClass
{
	private const string _plain = "plain.button";

	private static Task RunAsync(Func<Task> body) => HeadlessRun.OnUiThread(body);

	private static Window NewWindow()
	{
		var button = new Button { Content = "Plain" };

		AutomationProperties.SetAutomationId(button, _plain);

		return new Window { Name = "PlainWindow", Width = 300, Height = 200, Content = button };
	}

	private static UiAutomationService NewService(AutomationFixture fixture)
		=> new(
			new UiSessionInfo(
				"test", fixture.InstanceId, "1", "1.0", "1", "1", "fixture", true, "none",
				ImmutableArray<string>.Empty, UiReadBudget.Default),
			fixture.Nodes,
			fixture.Adapters,
			fixture.Roots,
			fixture.Snapshots,
			fixture.Revisions,
			fixture.Tree,
			fixture.Waits,
			fixture.Input,
			fixture.Journal,
			new UiDiagnosticBuffer(),
			new UiArtifactStore(),
			fixture.Executor);

	private static IEnumerable<(string Name, Func<UiAutomationService, UiTarget, Task> Ask)> Readings()
	{
		var page = new UiPageRequest(10, null);

		yield return ("grid.columns", (service, target)
			=> service.ReadGridColumnsAsync(target, new GridColumnsQuery([], page, null), CancellationToken.None));
		yield return ("grid.rows", (service, target)
			=> service.ReadGridRowsAsync(
				target, new GridRowsQuery(GridRowsModes.ViewData, null, [], [], page, null), CancellationToken.None));
		yield return ("grid.groups", (service, target)
			=> service.ReadGridGroupsAsync(target, new GridGroupsQuery(null, page, null), CancellationToken.None));
		yield return ("chart.series", (service, target)
			=> service.ReadChartSeriesAsync(target, new ChartSeriesQuery(page, null), CancellationToken.None));
		yield return ("chart.points", (service, target)
			=> service.ReadChartPointsAsync(
				target, new ChartPointsQuery("any", null, null, null, [], page, null), CancellationToken.None));
		yield return ("orderBook.levels", (service, target)
			=> service.ReadOrderBookLevelsAsync(
				target, new OrderBookLevelsQuery(null, page, null), CancellationToken.None));
		yield return ("propertyEditor.items", (service, target)
			=> service.ReadPropertyEditorItemsAsync(
				target, new PropertyEditorItemsQuery([], page, null), CancellationToken.None));
		yield return ("tree.items", (service, target)
			=> service.ReadTreeItemsAsync(target, new TreeItemsQuery(null, [], page, null), CancellationToken.None));
		yield return ("document.content", (service, target)
			=> service.ReadDocumentContentAsync(
				target, new DocumentContentQuery(null, page, null), CancellationToken.None));
		yield return ("diagram.nodes", (service, target)
			=> service.ReadDiagramNodesAsync(target, new DiagramNodesQuery([], page, null), CancellationToken.None));
		yield return ("diagram.connections", (service, target)
			=> service.ReadDiagramConnectionsAsync(
				target, new DiagramConnectionsQuery(null, [], page, null), CancellationToken.None));
		yield return ("dock.layout", (service, target)
			=> service.ReadDockLayoutAsync(
				target, new DockLayoutQuery(null, UiReadBudget.Default, null), CancellationToken.None));
	}

	[TestMethod]
	[Timeout(60000)]
	public Task EveryReadingRefusesAControlThatIsNotOfItsKind() => RunAsync(async () =>
	{
		var window = NewWindow();

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame()?.Dispose();

		using var fixture = new AutomationFixture(window);

		try
		{
			var service = NewService(fixture);
			var target = UiTarget.FromId(new UiNodeId("window:PlainWindow", _plain));

			foreach (var (name, ask) in Readings())
			{
				var error = await ThrowsAsync<UiAutomationException>(
					() => ask(service, target), $"{name} answered for a plain button.");

				AreEqual(UiErrorCodes.UnsupportedCapability, error.Error.Code, name);
			}
		}
		finally
		{
			window.Close();
		}
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AControlThatIsNotThereIsNotFoundRatherThanUnsupported() => RunAsync(async () =>
	{
		var window = NewWindow();

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame()?.Dispose();

		using var fixture = new AutomationFixture(window);

		try
		{
			var service = NewService(fixture);

			// The two refusals mean different things to a caller: one says "look somewhere else", the other
			// says "this is not that kind of control". Answering the same code for both would make the
			// first one impossible to act on.
			var error = await ThrowsAsync<UiAutomationException>(
				() => service.ReadGridRowsAsync(
					UiTarget.FromId(new UiNodeId("window:PlainWindow", "nobody")),
					new GridRowsQuery(GridRowsModes.ViewData, null, [], [], new UiPageRequest(10, null), null),
					CancellationToken.None),
				"A node nobody registered was answered for.");

			AreEqual(UiErrorCodes.NotFound, error.Error.Code);
		}
		finally
		{
			window.Close();
		}
	});
}
