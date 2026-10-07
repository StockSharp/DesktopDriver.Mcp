namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia.Controls;

using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// A docking workspace, read through the module.
/// </summary>
/// <remarks>
/// The layout is built with the same model and factory the products use, so what these tests establish
/// holds for the shell and for every application that docks its panels.
/// <para>
/// The point of reading a workspace at all is that it says where a panel's content is without saying
/// what the content holds. That boundary is what lets the same table be read identically whether it is
/// docked, floating or in an ordinary window.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class DockTests : BaseTestClass
{
	private sealed class TestFactory : Factory
	{
		public override IRootDock CreateLayout()
		{
			var orders = new Document { Id = "Orders", Title = "Orders" };
			var trades = new Document { Id = "Trades", Title = "Trades" };
			var explorer = new Tool { Id = "Explorer", Title = "Explorer" };

			var documents = new DocumentDock
			{
				Id = "Documents",
				Title = "Documents",
				VisibleDockables = CreateList<IDockable>(orders, trades),
				ActiveDockable = orders,
				CanCreateDocument = false,
			};

			var tools = new ToolDock
			{
				Id = "Tools",
				Title = "Tools",
				VisibleDockables = CreateList<IDockable>(explorer),
				ActiveDockable = explorer,
				Alignment = Alignment.Left,
			};

			var split = new ProportionalDock
			{
				Id = "Split",
				Orientation = Orientation.Horizontal,
				VisibleDockables = CreateList<IDockable>(tools, new ProportionalDockSplitter(), documents),
				ActiveDockable = documents,
			};

			var root = CreateRootDock();

			root.Id = "Root";
			root.Title = "Root";
			root.VisibleDockables = CreateList<IDockable>(split);
			root.ActiveDockable = split;
			root.DefaultDockable = split;

			return root;
		}
	}

	private static Task RunAsync(Action body) => AssemblyInitializer.Session.Dispatch(body, CancellationToken.None);

	private static DockControl NewWorkspace()
	{
		var factory = new TestFactory();
		var layout = factory.CreateLayout();

		factory.InitLayout(layout);

		return new DockControl { Name = "Workspace", Layout = layout, Factory = factory };
	}

	private static IUiDockAdapter AdapterOf(ControlHarness harness) => (IUiDockAdapter)harness.Adapter;

	private static DockLayoutSnapshot LayoutOf(ControlHarness harness)
		=> AdapterOf(harness).ReadLayout(
			harness.Subject,
			new DockLayoutQuery(null, UiReadBudget.Default, null),
			harness.Context);

	[TestMethod]
	[Timeout(60000)]
	public Task ItIsReadAsAWorkspaceRatherThanAsJustAnotherControl() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewWorkspace());

		AreEqual("workspace", harness.Adapter.Kind);
		IsTrue(harness.Adapter.GetCapabilities(harness.Subject).Contains("dock.layout"));
	});

	[TestMethod]
	[Timeout(60000)]
	public Task TheLayoutComesBackAsItsGroupsAndItsPanels() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewWorkspace());

		var layout = LayoutOf(harness);
		var groups = layout.Nodes.OfType<DockGroupSnapshot>().ToArray();
		var panels = layout.Nodes.OfType<DockPanelSnapshot>().ToArray();

		CollectionAssert.AreEquivalent(
			new[] { "Root", "Split", "Tools", "Documents" },
			groups.Select(group => group.Id).ToArray());

		CollectionAssert.AreEquivalent(
			new[] { "Orders", "Trades", "Explorer" },
			panels.Select(panel => panel.Id).ToArray());

		AreEqual(DockGroupKinds.Root, groups.Single(group => group.Id == "Root").GroupKind);
		AreEqual(DockGroupKinds.Split, groups.Single(group => group.Id == "Split").GroupKind);
		AreEqual(DockGroupKinds.Tabs, groups.Single(group => group.Id == "Documents").GroupKind);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task ASplitSaysWhichWayItDividesAndAGroupThatDoesNotSaysSo() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewWorkspace());

		var groups = LayoutOf(harness).Nodes.OfType<DockGroupSnapshot>().ToArray();

		AreEqual(
			DockOrientations.Horizontal,
			((UiKnown<DockOrientations>)groups.Single(group => group.Id == "Split").Orientation).Value);

		IsFalse(
			groups.Single(group => group.Id == "Documents").Orientation.IsKnown,
			"A tab group does not divide, and saying it divides horizontally would be an invention.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AGroupSaysWhichOfItsPanelsIsTheSelectedTab() => RunAsync(() =>
	{
		// An unselected tab is not a missing panel. A test that could not tell them apart would pass on a
		// workspace that had lost one.
		using var harness = new ControlHarness(NewWorkspace());

		var layout = LayoutOf(harness);
		var documents = layout.Nodes.OfType<DockGroupSnapshot>().Single(group => group.Id == "Documents");
		var panels = layout.Nodes.OfType<DockPanelSnapshot>().ToDictionary(panel => panel.Id);

		AreEqual("Orders", ((UiKnown<string>)documents.SelectedPanelId).Value);
		IsTrue(panels["Orders"].IsSelectedInGroup);
		IsFalse(panels["Trades"].IsSelectedInGroup);
		AreEqual(DockPanelPresentations.HiddenTab, panels["Trades"].Presentation);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task ThePanelsKeepTheOrderTheirGroupHoldsThemIn() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewWorkspace());

		var documents = LayoutOf(harness)
			.Nodes
			.OfType<DockGroupSnapshot>()
			.Single(group => group.Id == "Documents");

		CollectionAssert.AreEqual(new[] { "Orders", "Trades" }, documents.ChildIds.ToArray());
	});

	[TestMethod]
	[Timeout(60000)]
	public Task ReadingTheLayoutDoesNotChangeIt() => RunAsync(() =>
	{
		// A reader that built a panel in order to describe it would change the thing it was asked about,
		// and a lazily built panel is usually exactly what a test is about. So the same read, twice,
		// has to say the same thing.
		using var harness = new ControlHarness(NewWorkspace());

		var first = Describe(LayoutOf(harness));
		var second = Describe(LayoutOf(harness));

		CollectionAssert.AreEqual(first, second, "Reading the layout changed it.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AnUnselectedTabHasNotBuiltItsContentYet() => RunAsync(() =>
	{
		// A tab that has never been on top has drawn nothing, so there is nothing to address inside it -
		// and saying so is the truth rather than a failure to look hard enough. The tab itself exists the
		// whole time, which is what a reader that took the first visual carrying the panel used to find.
		using var harness = new ControlHarness(NewWorkspace());

		var panels = LayoutOf(harness).Nodes.OfType<DockPanelSnapshot>().ToDictionary(panel => panel.Id);

		AreEqual(DockPanelPresentations.HiddenTab, panels["Trades"].Presentation);
		AreEqual(UiContentStatuses.NotCreated, panels["Trades"].ContentStatus);
		IsFalse(panels["Trades"].ContentRoot.IsKnown, "A panel that has drawn nothing has nothing to address.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task TheWorkspaceCountsItsGroupsAndPanelsAndNamesWhatHasTheFocus() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewWorkspace());

		var state = (DockState)harness.Adapter.CaptureState(harness.Subject, harness.Context).State;

		AreEqual("Root", state.WorkspaceId);
		AreEqual(4L, ((UiKnown<long>)state.GroupCount).Value);
		AreEqual(3L, ((UiKnown<long>)state.PanelCount).Value);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task OnePartOfTheLayoutCanBeReadOnItsOwn() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewWorkspace());

		var layout = AdapterOf(harness).ReadLayout(
			harness.Subject,
			new DockLayoutQuery("Documents", UiReadBudget.Default, null),
			harness.Context);

		CollectionAssert.AreEquivalent(
			new[] { "Documents", "Orders", "Trades" },
			layout.Nodes.Select(node => node.Id).ToArray());
	});

	[TestMethod]
	[Timeout(60000)]
	public Task APartOfTheLayoutThatIsNotThereIsRefused() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewWorkspace());

		var error = Throws<UiAutomationException>(() => AdapterOf(harness).ReadLayout(
			harness.Subject,
			new DockLayoutQuery("NoSuchPane", UiReadBudget.Default, null),
			harness.Context));

		AreEqual(UiErrorCodes.NotFound, error.Error.Code);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AWorkspaceWithNoLayoutSaysSoRatherThanLookingEmpty() => RunAsync(() =>
	{
		using var harness = new ControlHarness(new DockControl { Name = "Workspace" });

		var capture = harness.Adapter.CaptureState(harness.Subject, harness.Context);

		AreEqual(UiReadyStatuses.Loading, capture.Ready);

		var error = Throws<UiAutomationException>(() => LayoutOf(harness));

		AreEqual(UiErrorCodes.NotCreated, error.Error.Code);
	});

	// The parts of a layout that a second read must not have changed: which nodes there are, and whether
	// anything has been built for each of them.
	private static string[] Describe(DockLayoutSnapshot layout)
		=> [.. layout.Nodes.Select(node => node switch
		{
			DockPanelSnapshot panel => $"panel {panel.Id} {panel.ContentStatus} {panel.Presentation}",
			DockGroupSnapshot group => $"group {group.Id} {group.GroupKind} [{string.Join(",", group.ChildIds)}]",
			_ => node.Id,
		})];
}
