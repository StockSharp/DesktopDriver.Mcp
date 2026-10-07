namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Ecng.UnitTesting;

using global::Avalonia.Controls;
using global::Avalonia.Controls.Templates;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// A tree, read through the module.
/// </summary>
/// <remarks>
/// An item is named by its path through the tree, so these check that the path is what it says it is
/// and that it survives the things that move items about. They also check the boundary a tree has and a
/// list does not: what is inside a closed item has not been built, and reading it would be opening it.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class TreeTests : BaseTestClass
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
				new Source("Connector", [new Source("Transport", []), new Source("Heartbeat", [])]),
				new Source("Strategies", [new Source("Sma", [new Source("Errors", [])])]),
				new Source("Storage", []),
			},
		};

		tree.ItemTemplate = new FuncTreeDataTemplate<Source>(
			(source, _) => new TextBlock { Text = source.Name },
			source => source.Children);

		return tree;
	}

	private static IUiTreeAdapter AdapterOf(ControlHarness harness) => (IUiTreeAdapter)harness.Adapter;

	private static TreeState StateOf(ControlHarness harness)
		=> (TreeState)harness.Adapter.CaptureState(harness.Subject, harness.Context).State;

	private static TreeItemSnapshot[] ItemsOf(ControlHarness harness, string parent = null)
		=> [.. AdapterOf(harness).ReadItems(
			harness.Subject,
			new TreeItemsQuery(parent, [], new UiPageRequest(200, null), null),
			harness.Context).Items];

	private static void Open(ControlHarness harness, string text)
	{
		var tree = (TreeView)harness.Control;

		foreach (var container in Containers(tree))
		{
			if (HeaderOf(container) == text)
				container.IsExpanded = true;
		}

		harness.Settle();
	}

	// The header is the data item the template was given, not the TextBlock the template built: what a
	// person reads is its own text.
	private static string HeaderOf(TreeViewItem container)
		=> (container.Header as TextBlock)?.Text ?? container.Header?.ToString();

	private static IEnumerable<TreeViewItem> Containers(ItemsControl owner)
	{
		foreach (var container in owner.GetRealizedContainers().OfType<TreeViewItem>())
		{
			yield return container;

			foreach (var nested in Containers(container))
				yield return nested;
		}
	}

	[TestMethod]
	[Timeout(60000)]
	public Task ItIsReadAsATreeRatherThanAsJustAnotherList() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewTree());

		AreEqual("tree", harness.Adapter.Kind);
		IsTrue(harness.Adapter.GetCapabilities(harness.Subject).Contains("tree.items"));
	});

	[TestMethod]
	[Timeout(60000)]
	public Task TheTopLevelIsWhatIsOnShowUntilSomethingIsOpened() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewTree());

		var items = ItemsOf(harness);

		AreEqual(3, items.Length);
		AreEqual("Connector", items[0].Key);
		AreEqual(0, items[0].Depth);
		IsFalse(items[0].IsExpanded);

		// The counts are known even for a closed item: a tree knows how many things are inside one
		// without having built them, and answering "none" would be a different claim.
		IsTrue(items[0].HasChildren);
		AreEqual(2L, ((UiKnown<long>)items[0].ChildCount).Value);
		IsFalse(items[2].HasChildren);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task WhatIsInsideAClosedItemIsNotOnShow() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewTree());

		// Reading them would be building them, and building them is what opening an item does.
		IsFalse(ItemsOf(harness).Any(item => item.Key.Contains('/')));
	});

	[TestMethod]
	[Timeout(60000)]
	public Task OpeningAnItemPutsWhatIsInsideItOnShow() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewTree());

		Open(harness, "Connector");

		var items = ItemsOf(harness);
		var inside = items.Where(item => item.ParentKey == "Connector").ToArray();

		AreEqual(2, inside.Length);
		AreEqual("Connector/Transport", inside[0].Key);
		AreEqual(1, inside[0].Depth);
		IsTrue(items.Single(item => item.Key == "Connector").IsExpanded);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task APathIsAPathAllTheWayDown() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewTree());

		Open(harness, "Strategies");
		Open(harness, "Sma");

		var items = ItemsOf(harness);

		IsTrue(items.Any(item => item.Key == "Strategies/Sma/Errors"), string.Join(", ", items.Select(i => i.Key)));
		AreEqual(2, items.Single(item => item.Key == "Strategies/Sma/Errors").Depth);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task NamingAParentAnswersWithWhatIsDirectlyInsideIt() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewTree());

		Open(harness, "Strategies");
		Open(harness, "Sma");

		var inside = ItemsOf(harness, "Strategies");

		AreEqual(1, inside.Length);
		AreEqual("Strategies/Sma", inside[0].Key);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task TheStateCountsWhatIsOnShowRatherThanWhatTheTreeHolds() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewTree());

		var closed = StateOf(harness);

		AreEqual(3L, ((UiKnown<long>)closed.RootCount).Value);
		AreEqual(3L, ((UiKnown<long>)closed.VisibleItemCount).Value);
		AreEqual(0L, ((UiKnown<long>)closed.ExpandedCount).Value);

		Open(harness, "Connector");

		var opened = StateOf(harness);

		AreEqual(5L, ((UiKnown<long>)opened.VisibleItemCount).Value);
		AreEqual(1L, ((UiKnown<long>)opened.ExpandedCount).Value);
		AreEqual(3L, ((UiKnown<long>)opened.RootCount).Value);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task SelectingAnItemIsReportedByItsPath() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewTree());
		var tree = (TreeView)harness.Control;

		Open(harness, "Connector");

		var container = Containers(tree).First(item => HeaderOf(item) == "Transport");

		container.IsSelected = true;
		harness.Settle();

		var state = StateOf(harness);

		AreEqual("Connector/Transport", ((UiKnown<string>)state.SelectedKey).Value);
		AreEqual(1L, ((UiKnown<long>)state.SelectedCount).Value);
		IsTrue(ItemsOf(harness).Single(item => item.Key == "Connector/Transport").IsSelected);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task TwoItemsThatReadTheSameAreStillTwoItems() => RunAsync(() =>
	{
		var tree = NewTree();

		tree.ItemsSource = new[]
		{
			new Source("Errors", []),
			new Source("Errors", []),
		};

		using var harness = new ControlHarness(tree);

		var items = ItemsOf(harness);

		AreEqual(2, items.Length);
		AreEqual("Errors", items[0].Key);

		// Naming the second one has to name the second one, so it gets a number rather than the same key.
		AreEqual("Errors#2", items[1].Key);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AskingForOneItemAnswersWithThatOne() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewTree());

		var wanted = AdapterOf(harness).ReadItems(
			harness.Subject,
			new TreeItemsQuery(null, ["Storage"], new UiPageRequest(50, null), null),
			harness.Context);

		AreEqual(1, wanted.Items.Length);
		AreEqual("Storage", wanted.Items[0].Key);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task APageThatWasCutShortSaysSo() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewTree());

		var page = AdapterOf(harness).ReadItems(
			harness.Subject,
			new TreeItemsQuery(null, [], new UiPageRequest(2, null), null),
			harness.Context);

		AreEqual(2, page.Items.Length);
		AreEqual(3L, ((UiKnown<long>)page.Total).Value);
		IsTrue(page.Truncated);
		IsNotNull(page.TruncationReason);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task ReadingATreeDoesNotOpenAnythingInIt() => RunAsync(() =>
	{
		using var harness = new ControlHarness(NewTree());

		var before = ItemsOf(harness);

		ItemsOf(harness);
		ItemsOf(harness);

		var after = ItemsOf(harness);

		AreEqual(before.Length, after.Length);
		IsFalse(after.Any(item => item.IsExpanded));
	});
}
