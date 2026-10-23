namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Controls.Templates;
using global::Avalonia.Media;
using global::Avalonia.Styling;
using global::Avalonia.VisualTree;

using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

// The docking model has an orientation of its own.
using HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment;
using VerticalAlignment = global::Avalonia.Layout.VerticalAlignment;

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

	// A panel whose content is a control of its own, so that what it holds has an address to look for.
	private sealed class PanelTool : Tool
	{
		public Control Body { get; init; }
	}

	private static Task RunAsync(Action body) => AssemblyInitializer.Session.Dispatch(body, CancellationToken.None);

	// A workspace with a panel tucked away at its bottom edge, the way an application keeps its log.
	private static (DockControl Workspace, Factory Factory, PanelTool Log) NewWorkspaceWithAnEdgePanel()
	{
		var factory = new TestFactory();
		var layout = factory.CreateLayout();
		var log = new PanelTool
		{
			Id = "Log",
			Title = "Log",
			Body = new Button { Name = "LogButton", Content = "Log" },
		};

		layout.BottomPinnedDockables = factory.CreateList<IDockable>(log);
		factory.InitLayout(layout);

		var workspace = new DockControl { Name = "Workspace", Layout = layout, Factory = factory };
		workspace.DataTemplates.Add(new FuncDataTemplate<PanelTool>((tool, _) => tool.Body, false));

		return (workspace, factory, log);
	}

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
	public Task APanelTuckedAwayAtAnEdgeIsInTheLayoutAsAutoHidden() => RunAsync(() =>
	{
		// The panel is in no group: it hangs off the root at an edge, and a reader that walked only the groups
		// lost it - together with everything it holds once it slides out.
		var (workspace, _, _) = NewWorkspaceWithAnEdgePanel();
		using var harness = new ControlHarness(workspace);

		var log = LayoutOf(harness).Nodes.OfType<DockPanelSnapshot>().SingleOrDefault(panel => panel.Id == "Log");

		IsNotNull(log, "A panel tucked away at an edge is missing from the layout.");
		AreEqual("Root", log.ParentId);
		AreEqual(DockPanelPresentations.AutoHidden, log.Presentation);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task APanelSlidOutOfItsEdgeIsShownAndWhatItHoldsHasAnAddress() => RunAsync(() =>
	{
		var (workspace, factory, log) = NewWorkspaceWithAnEdgePanel();
		using var harness = new ControlHarness(workspace);

		factory.PreviewPinnedDockable(log);
		harness.Settle();
		harness.Settle();

		var panel = LayoutOf(harness).Nodes.OfType<DockPanelSnapshot>().Single(node => node.Id == "Log");

		AreEqual(DockPanelPresentations.Shown, panel.Presentation);
		IsTrue(panel.ContentRoot.IsKnown, "A panel slid out of its edge has nothing to address.");

		var tree = harness.Tree.Build(new UiTreeQuery(
			null,
			false,
			UiCaptureOptions.Default with { Budget = new UiReadBudget(64, 20000, 2000) }));

		IsTrue(
			tree.Nodes.Any(node => node.Node.Id.LocalId == "LogButton"),
			"What a panel slid out of its edge holds is not in the tree.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task APanelAtAnEdgeIsPressedByItsButtonOnThatEdge() => RunAsync(() =>
	{
		// What a person presses to slide the panel out is its button on the edge, so that is what its tab is.
		var (workspace, _, log) = NewWorkspaceWithAnEdgePanel();
		using var harness = new ControlHarness(workspace);

		var adapter = (IUiInputTargetAdapter)harness.Adapter;
		var part = new UiDockTabPart("Log");

		IsTrue(adapter.SupportsTargetPart(harness.Subject, part));

		var target = adapter.ResolveInputTarget(harness.Subject, part, new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.None), harness.Context);

		var button = workspace.GetVisualDescendants()
			.OfType<ToolPinItemControl>()
			.Single(item => ReferenceEquals(item.DataContext, log));
		var origin = button.TranslatePoint(default, harness.Window) ?? throw new InvalidOperationException("The button is not laid out.");

		AreEqual(origin.X, target.BoundsInSurfaceDip.X, 0.5);
		AreEqual(origin.Y, target.BoundsInSurfaceDip.Y, 0.5);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task ATabIsPressedWhereNoneOfItsOwnButtonsIs() => RunAsync(() =>
	{
		// A tab short enough to have the button it carries - help, close - at its middle: pressing that button instead of
		// the tab opens the help or closes the panel, and the panel never comes to the front.
		var workspace = NewWorkspace();

		workspace.Resources[typeof(DocumentTabStripItem)] = new ControlTheme(typeof(DocumentTabStripItem))
		{
			Setters =
			{
				new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<DocumentTabStripItem>((_, _) => new Panel
				{
					Width = 60,
					Height = 24,
					Background = Brushes.Transparent,
					Children =
					{
						new TextBlock { Text = "Tab", HorizontalAlignment = HorizontalAlignment.Left },
						new Button { Name = "TabHelp", Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Center },
					},
				})),
			},
		};

		using var harness = new ControlHarness(workspace);

		var target = ((IUiInputTargetAdapter)harness.Adapter).ResolveInputTarget(
			harness.Subject, new UiDockTabPart("Trades"), new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.None), harness.Context);

		var help = workspace.GetVisualDescendants()
			.OfType<Button>()
			.Single(button => button.Name == "TabHelp" && button.FindAncestorOfType<DocumentTabStripItem>() is { DataContext: IDockable { Id: "Trades" } });
		var bounds = new Rect(help.TranslatePoint(default, harness.Window) ?? throw new InvalidOperationException("The button is not laid out."), help.Bounds.Size);

		IsFalse(bounds.Contains(new Point(target.PointInSurfaceDip.X, target.PointInSurfaceDip.Y)), "The tab is pressed on the button it carries.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task APanelAtASideEdgeIsPressedByItsButtonTurnedAlongThatEdge() => RunAsync(() =>
	{
		// Along a side edge the button is turned on its side; it is pressed all the same.
		var factory = new TestFactory();
		var layout = factory.CreateLayout();
		var parameters = new PanelTool { Id = "Parameters", Title = "Parameters", Body = new Button { Name = "ParametersButton", Content = "Parameters" } };

		layout.RightPinnedDockables = factory.CreateList<IDockable>(parameters);
		factory.InitLayout(layout);

		var workspace = new DockControl { Name = "Workspace", Layout = layout, Factory = factory };
		workspace.DataTemplates.Add(new FuncDataTemplate<PanelTool>((tool, _) => tool.Body, false));

		using var harness = new ControlHarness(workspace);

		var target = ((IUiInputTargetAdapter)harness.Adapter).ResolveInputTarget(
			harness.Subject, new UiDockTabPart("Parameters"), new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.None), harness.Context);

		var button = workspace.GetVisualDescendants()
			.OfType<ToolPinItemControl>()
			.Single(item => ReferenceEquals(item.DataContext, parameters));
		var bounds = new Rect(button.TranslatePoint(default, harness.Window) ?? throw new InvalidOperationException("The button is not laid out."), button.Bounds.Size);

		IsTrue(bounds.Contains(new Point(target.PointInSurfaceDip.X, target.PointInSurfaceDip.Y)), "The button on the side edge is not where the press goes.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task ATabTurnedOnItsSideIsPressedOnItsTitle() => RunAsync(() =>
	{
		// A tab along a side edge whose button - its title, turned along the edge - takes up a corner of it and leaves the
		// rest bare: its middle and the points in from its edges are nothing to press, its title is what a person presses.
		var factory = new TestFactory();
		var layout = factory.CreateLayout();
		var parameters = new PanelTool { Id = "Parameters", Title = "Parameters of the strategy", Body = new Button { Content = "Body" } };

		layout.RightPinnedDockables = factory.CreateList<IDockable>(parameters);
		factory.InitLayout(layout);

		var workspace = new DockControl { Name = "Workspace", Layout = layout, Factory = factory };
		workspace.DataTemplates.Add(new FuncDataTemplate<PanelTool>((tool, _) => tool.Body, false));

		workspace.Resources[typeof(ToolPinItemControl)] = new ControlTheme(typeof(ToolPinItemControl))
		{
			Setters =
			{
				new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<ToolPinItemControl>((item, _) => new Panel
				{
					Width = 300,
					Height = 300,
					Children =
					{
						new LayoutTransformControl
						{
							LayoutTransform = new RotateTransform(-90),
							HorizontalAlignment = HorizontalAlignment.Right,
							VerticalAlignment = VerticalAlignment.Top,
							Margin = new Thickness(0, 30, 30, 0),
							Child = new Button
							{
								Name = "PinButton",
								Content = new TextBlock { Text = ((IDockable)item.DataContext).Title },
							},
						},
					},
				})),
			},
		};

		using var harness = new ControlHarness(workspace);

		var target = ((IUiInputTargetAdapter)harness.Adapter).ResolveInputTarget(
			harness.Subject, new UiDockTabPart("Parameters"), new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.None), harness.Context);

		var button = workspace.GetVisualDescendants().OfType<Button>().Single(candidate => candidate.Name == "PinButton");
		var drawn = new Rect(button.Bounds.Size).TransformToAABB(button.TransformToVisual(harness.Window) ?? throw new InvalidOperationException("The button is not laid out."));

		IsTrue(drawn.Contains(new Point(target.PointInSurfaceDip.X, target.PointInSurfaceDip.Y)), "The tab is not pressed on its title.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task APanelShownAloneIsClosedByTheButtonOnItsCaption() => RunAsync(() =>
	{
		// A tool alone in its group shows no tab: the button a person closes it with is on the caption over it.
		var workspace = NewWorkspace();
		using var harness = new ControlHarness(workspace);

		var target = ((IUiInputTargetAdapter)harness.Adapter).ResolveInputTarget(
			harness.Subject, new UiDockClosePart("Explorer"), new UiClickAction(UiPointerButtons.Left, 1, UiKeyModifiers.None), harness.Context);

		var close = workspace.GetVisualDescendants()
			.OfType<ToolChromeControl>()
			.Single()
			.GetVisualDescendants()
			.OfType<Button>()
			.Single(button => button.Name == "PART_CloseButton");
		var bounds = new Rect(close.TranslatePoint(default, harness.Window) ?? throw new InvalidOperationException("The button is not laid out."), close.Bounds.Size);

		IsTrue(bounds.Contains(new Point(target.PointInSurfaceDip.X, target.PointInSurfaceDip.Y)), "The panel is not closed by the button on its caption.");
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
