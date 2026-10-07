namespace StockSharp.DesktopDriver.Tests;

using System;
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
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Reading only what the caller believes it is reading.
/// </summary>
/// <remarks>
/// A guard is how a caller says "answer only if this is still the state I saw". Without it a runner
/// that sorts a table and then asks for its rows can be handed the rows from before the sort, and the
/// two arrive looking like one consistent answer - which is the failure a test would never notice.
/// <para>
/// Every query in this protocol has carried a guard from the first day. Until this test existed,
/// nothing on the paged path ever read it.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class ReadGuardTests : BaseTestClass
{
	private const string _tree = "guarded.tree";

	private static Task RunAsync(Func<Task> body) => HeadlessRun.OnUiThread(body);

	private static Window NewWindow()
	{
		var tree = new TreeView { ItemsSource = new[] { "one", "two", "three" } };

		AutomationProperties.SetAutomationId(tree, _tree);

		return new Window { Name = "GuardWindow", Width = 300, Height = 200, Content = tree };
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

	private static async Task<T> WithWindowAsync<T>(Func<AutomationFixture, UiTarget, Task<T>> body)
	{
		var window = NewWindow();

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame()?.Dispose();

		using var fixture = new AutomationFixture(window);

		try
		{
			return await body(fixture, UiTarget.FromId(new UiNodeId("window:GuardWindow", _tree)));
		}
		finally
		{
			window.Close();
		}
	}

	private static TreeItemsQuery Items(UiReadGuard guard)
		=> new(null, [], new UiPageRequest(50, null), guard);

	[TestMethod]
	[Timeout(60000)]
	public Task AGuardFromAnotherSourceIsRefused() => RunAsync(() => WithWindowAsync(async (fixture, target) =>
	{
		var service = NewService(fixture);

		// A different epoch means the control behind this address was replaced. Whatever the caller
		// measured, it measured on something else.
		var guard = new UiReadGuard(new UiRevision(Guid.NewGuid(), 0), null, null);

		var error = await ThrowsAsync<UiAutomationException>(
			() => service.ReadTreeItemsAsync(target, Items(guard), CancellationToken.None),
			"A paged read answered under a guard that names another source.");

		AreEqual(UiErrorCodes.StateChanged, error.Error.Code);

		return true;
	}));

	[TestMethod]
	[Timeout(60000)]
	public Task AGuardThatHasMovedOnIsRefused() => RunAsync(() => WithWindowAsync(async (fixture, target) =>
	{
		var service = NewService(fixture);
		var now = fixture.Revisions.Read(new UiNodeId("window:GuardWindow", _tree));

		// The same source, an older version: exactly the case a runner hits after it changed something.
		var stale = new UiReadGuard(new UiRevision(now.State.Epoch, now.State.Version + 1), null, null);

		var error = await ThrowsAsync<UiAutomationException>(
			() => service.ReadTreeItemsAsync(target, Items(stale), CancellationToken.None),
			"A paged read answered under a guard that no longer matches.");

		AreEqual(UiErrorCodes.StateChanged, error.Error.Code);

		return true;
	}));

	[TestMethod]
	[Timeout(60000)]
	public Task AGuardThatStillHoldsIsAnswered() => RunAsync(() => WithWindowAsync(async (fixture, target) =>
	{
		var service = NewService(fixture);
		var now = fixture.Revisions.Read(new UiNodeId("window:GuardWindow", _tree));
		var guard = new UiReadGuard(now.State, now.View, now.Layout);

		var page = await service.ReadTreeItemsAsync(target, Items(guard), CancellationToken.None);

		AreEqual(3, page.Items.Length);

		return true;
	}));

	[TestMethod]
	[Timeout(60000)]
	public Task AReadWithNoGuardIsOneLookAndSaysSo() => RunAsync(() => WithWindowAsync(async (fixture, target) =>
	{
		var service = NewService(fixture);

		// Asking without a guard is legitimate - it just means the caller accepts whatever is there now.
		var page = await service.ReadTreeItemsAsync(target, Items(null), CancellationToken.None);

		AreEqual(3, page.Items.Length);

		return true;
	}));
}
