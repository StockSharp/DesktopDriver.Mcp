namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Ecng.UnitTesting;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Presenters;
using global::Avalonia.Controls.Templates;
using global::Avalonia.VisualTree;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// An item of a tree, and a control inside one - the switch beside a source, the button beside a group.
/// </summary>
/// <remarks>
/// A tree holds items rather than nodes, so an item and what its row carries can only be reached by naming
/// the item by its path. A run that cannot select a source in an explorer, or switch one on, cannot drive the
/// explorer at all.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class TreeItemPartTests : BaseTestClass
{
	private sealed record Source(string Name, IReadOnlyList<Source> Children)
	{
		public override string ToString() => Name;
	}

	private static Task RunAsync(Action body) => AssemblyInitializer.Session.Dispatch(body, CancellationToken.None);

	private static TreeView NewTree()
	{
		var tree = new TreeView
		{
			Name = "Sources",
			ItemsSource = new[]
			{
				new Source("Downloads", [new Source("Server", []), new Source("Fix", [])]),
				new Source("Tools", []),
			},
		};

		tree.ItemTemplate = new FuncTreeDataTemplate<Source>(
			(source, _) =>
			{
				var row = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal };

				row.Children.Add(new TextBlock { Text = source.Name });
				row.Children.Add(new ToggleSwitch { Name = "EnabledSwitch", OnContent = string.Empty, OffContent = string.Empty, Width = 40 });

				return row;
			},
			source => source.Children);

		return tree;
	}

	private static void OpenAll(TreeView tree, ControlHarness harness)
	{
		foreach (var item in tree.GetVisualDescendants().OfType<TreeViewItem>())
			item.IsExpanded = true;

		harness.Settle();
	}

	private static UiResolvedInputTarget Resolve(ControlHarness harness, UiTargetPart part)
		=> ((IUiInputTargetAdapter)harness.Adapter).ResolveInputTarget(
			harness.Subject,
			part,
			new UiClickAction(UiPointerButtons.Left, 1),
			harness.Context);

	private static Point Origin(Visual visual, ControlHarness harness)
		=> visual.TranslatePoint(default, harness.Window) ?? throw new InvalidOperationException("It is not laid out.");

	[TestMethod]
	[Timeout(60000)]
	public Task AClickOnAnItemLandsOnItsOwnRowRatherThanOnItsChildren() => RunAsync(() =>
	{
		var tree = NewTree();

		using var harness = new ControlHarness(tree);

		OpenAll(tree, harness);

		var part = new UiTreeItemPart("Downloads");

		IsTrue(((IUiInputTargetAdapter)harness.Adapter).SupportsTargetPart(harness.Subject, part));

		var target = Resolve(harness, part);
		var item = tree.GetVisualDescendants().OfType<TreeViewItem>().First(candidate => candidate.DataContext is Source { Name: "Downloads" });
		var header = item.GetVisualDescendants().OfType<ContentPresenter>().First(presenter => presenter.Name == "PART_HeaderPresenter");

		AreEqual(Origin(header, harness).Y, target.BoundsInSurfaceDip.Y, 0.5);
		IsTrue(target.BoundsInSurfaceDip.Height < item.Bounds.Height, "The click would land on the children as well.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AControlInsideAnItemIsReachedByItsName() => RunAsync(() =>
	{
		var tree = NewTree();

		using var harness = new ControlHarness(tree);

		OpenAll(tree, harness);

		var target = Resolve(harness, new UiTreeItemControlPart("Downloads/Fix", "EnabledSwitch"));
		var item = tree.GetVisualDescendants().OfType<TreeViewItem>().First(candidate => candidate.DataContext is Source { Name: "Fix" });
		var toggle = item.GetVisualDescendants().OfType<ToggleSwitch>().First();

		AreEqual(Origin(toggle, harness).X, target.BoundsInSurfaceDip.X, 0.5);
		AreEqual(Origin(toggle, harness).Y, target.BoundsInSurfaceDip.Y, 0.5);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AnItemThatIsNotOnShowIsNotFound() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewTree());

		// Inside an item nobody has opened, and not an item at all.
		foreach (var key in new[] { "Downloads/Server", "Nothing" })
			AreEqual(UiErrorCodes.NotFound, Throws<UiAutomationException>(() => Resolve(harness, new UiTreeItemPart(key))).Error.Code, key);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AControlAnItemDoesNotHoldIsNotFound() => RunAsync(() =>
	{
		var tree = NewTree();

		using var harness = new ControlHarness(tree);

		OpenAll(tree, harness);

		AreEqual(
			UiErrorCodes.NotFound,
			Throws<UiAutomationException>(() => Resolve(harness, new UiTreeItemControlPart("Downloads", "NoSuchControl"))).Error.Code);
	});
}
