namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// What a workspace's revisions say when its panels move.
/// </summary>
/// <remarks>
/// Opening a panel from a menu and bringing a tab to the front are both things a caller does and then
/// waits to see the result of. The only thing there is to wait on is the workspace's view revision: the
/// layout it would read is the answer, so reading it cannot be what decides the answer has arrived. A
/// workspace whose view revision never moves leaves such a caller waiting until it times out on a
/// workspace that moved the panel immediately.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class DockRevisionTests : BaseTestClass
{
	private sealed class TestFactory : Factory
	{
		public override IRootDock CreateLayout()
		{
			var orders = new Document { Id = "Orders", Title = "Orders" };
			var trades = new Document { Id = "Trades", Title = "Trades" };

			var documents = new DocumentDock
			{
				Id = "Documents",
				Title = "Documents",
				VisibleDockables = CreateList<IDockable>(orders, trades),
				ActiveDockable = orders,
				CanCreateDocument = false,
			};

			var root = CreateRootDock();

			root.Id = "Root";
			root.Title = "Root";
			root.VisibleDockables = CreateList<IDockable>(documents);
			root.ActiveDockable = documents;
			root.DefaultDockable = documents;

			return root;
		}
	}

	private static Task RunAsync(Action body) => AssemblyInitializer.Session.Dispatch(body, CancellationToken.None);

	// Deliberately without a factory of its own: an application may hand the control one, or only a
	// layout that already carries the factory that built it. Hydra does the second.
	private static DockControl NewWorkspace(out TestFactory factory)
	{
		factory = new TestFactory();

		var layout = factory.CreateLayout();

		factory.InitLayout(layout);

		return new DockControl { Name = "Workspace", Layout = layout };
	}

	private static IDock DocumentsOf(DockControl workspace)
		=> ((IDock)workspace.Layout).VisibleDockables.OfType<IDock>().Single(dock => dock.Id == "Documents");

	[TestMethod]
	[Timeout(60000)]
	public Task BringingAPanelToTheFrontMovesTheWorkspaceViewRevision() => RunAsync(() =>
	{
		var workspace = NewWorkspace(out var factory);

		using var harness = new ControlHarness(workspace);

		var documents = DocumentsOf(workspace);
		var behind = documents.VisibleDockables.Single(dockable => dockable.Id == "Trades");

		var before = harness.Revisions.Read(harness.Id);

		factory.SetActiveDockable(behind);
		harness.Settle();

		IsTrue(
			harness.Revisions.Read(harness.Id).View.IsAfter(before.View),
			$"A panel came to the front and the view revision stayed at {before.View?.Version}.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task OpeningAPanelMovesTheWorkspaceViewRevision() => RunAsync(() =>
	{
		var workspace = NewWorkspace(out var factory);

		using var harness = new ControlHarness(workspace);

		var documents = DocumentsOf(workspace);

		var before = harness.Revisions.Read(harness.Id);

		factory.AddDockable(documents, new Document { Id = "Positions", Title = "Positions" });
		harness.Settle();

		IsTrue(
			harness.Revisions.Read(harness.Id).View.IsAfter(before.View),
			$"A panel was opened and the view revision stayed at {before.View?.Version}.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task ClosingAPanelMovesTheWorkspaceViewRevision() => RunAsync(() =>
	{
		var workspace = NewWorkspace(out var factory);

		using var harness = new ControlHarness(workspace);

		var documents = DocumentsOf(workspace);
		var closing = documents.VisibleDockables.Single(dockable => dockable.Id == "Trades");

		var before = harness.Revisions.Read(harness.Id);

		factory.RemoveDockable(closing, false);
		harness.Settle();

		IsTrue(
			harness.Revisions.Read(harness.Id).View.IsAfter(before.View),
			$"A panel was closed and the view revision stayed at {before.View?.Version}.");
	});
}
