namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Collections.Immutable;
using System.Linq;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Walking the interface: what comes back, and what the walk refuses to do.
/// </summary>
[TestClass]
public class SemanticTreeTests : BaseTestClass
{
	private sealed class Node : TestControl
	{
		public string Key { get; init; }
	}

	private sealed class Fixture
	{
		public UiNodeRegistry Nodes { get; } = new(Guid.NewGuid());
		public UiAdapterRegistry Adapters { get; } = new();
		public StubContainerAdapter Adapter { get; } = new("node", typeof(Node));
		public UiSemanticTreeBuilder Builder { get; }

		public Fixture(params string[] keys)
		{
			foreach (var key in keys)
				Nodes.Register(new UiSubject(Id(key), new Node { Key = key }), "node", true);

			Adapters.Register("node", typeof(Node), Adapter);

			var snapshots = new UiSnapshotService(
				Guid.NewGuid(), Nodes, Adapters, new UiRevisionTracker(), new StubPresentationReader(), new ImmediateExecutor());

			Builder = new UiSemanticTreeBuilder(
				Guid.NewGuid(),
				snapshots,
				Nodes,
				Adapters,
				new StubRootSource(Nodes.Resolve(UiTarget.FromId(Id(keys[0])))),
				new ImmediateExecutor());
		}

		public void Link(string parent, params string[] children)
		{
			var previous = Adapter.Children;

			Adapter.Children = subject => subject.Id.Equals(Id(parent))
				? [.. children.Select((child, index) => new UiChildLink(
					Id(parent),
					Nodes.GetReference(Id(child)),
					UiRelations.Child,
					index))]
				: previous(subject);
		}
	}

	private static UiNodeId Id(string key) => new("scope", key);

	[TestMethod]
	public void EveryNodeComesBackOnceAndTheEdgesSayWhereItSits()
	{
		// The same document can be shown in a tab and listed in a tree. One node, two edges - not two
		// copies a reader would have to notice are the same thing.
		var fixture = new Fixture("root", "a", "b", "shared");
		fixture.Link("root", "a", "b");
		fixture.Link("a", "shared");
		fixture.Link("b", "shared");

		var tree = fixture.Builder.Build(new UiTreeQuery(null, false, UiCaptureOptions.Default));

		AreEqual(4, tree.Nodes.Length);
		AreEqual(4, tree.Links.Length);
		AreEqual(1, tree.Nodes.Count(node => node.Node.Id.Equals(Id("shared"))));
		AreEqual(2, tree.Links.Count(link => link.Child.Id.Equals(Id("shared"))));
	}

	[TestMethod]
	public void ACycleIsCutRatherThanFollowed()
	{
		var fixture = new Fixture("root", "a");
		fixture.Link("root", "a");
		fixture.Link("a", "root");

		var tree = fixture.Builder.Build(new UiTreeQuery(null, false, UiCaptureOptions.Default));

		AreEqual(2, tree.Nodes.Length);
		AreEqual(2, tree.Links.Length);
	}

	[TestMethod]
	public void TheWalkStopsAtTheDepthItWasGivenAndSaysSo()
	{
		var fixture = new Fixture("root", "a", "b");
		fixture.Link("root", "a");
		fixture.Link("a", "b");

		var tree = fixture.Builder.Build(new UiTreeQuery(
			null,
			false,
			UiCaptureOptions.Default with { Budget = new UiReadBudget(MaxDepth: 1) }));

		AreEqual(2, tree.Nodes.Length);
		IsTrue(tree.Truncated);
	}

	[TestMethod]
	public void TheWalkStopsAtTheNodeCountItWasGivenAndSaysWhy()
	{
		var fixture = new Fixture("root", "a", "b");
		fixture.Link("root", "a", "b");

		var tree = fixture.Builder.Build(new UiTreeQuery(
			null,
			false,
			UiCaptureOptions.Default with { Budget = new UiReadBudget(MaxNodes: 2) }));

		AreEqual(2, tree.Nodes.Length);
		IsTrue(tree.Truncated);
		AreEqual(1, tree.Warnings.Length);
	}

	[TestMethod]
	public void ANodeOwnedElsewhereIsPointedAtRatherThanWalkedInto()
	{
		// Following every reference would drag the whole application into a reply about one panel.
		var fixture = new Fixture("root", "owned");
		fixture.Adapter.Children = subject => subject.Id.Equals(Id("root"))
			? [new UiChildLink(Id("root"), fixture.Nodes.GetReference(Id("owned")), UiRelations.OwnedReference, 0)]
			: ImmutableArray<UiChildLink>.Empty;

		var tree = fixture.Builder.Build(new UiTreeQuery(null, false, UiCaptureOptions.Default));

		AreEqual(1, tree.Nodes.Length);
		AreEqual(1, tree.Links.Length);
	}

	[TestMethod]
	public void AnEdgeToSomethingUnregisteredIsReportedRatherThanFollowed()
	{
		var fixture = new Fixture("root");
		fixture.Adapter.Children = subject => subject.Id.Equals(Id("root"))
			? [new UiChildLink(Id("root"), new UiNodeRef(Id("ghost"), null, "node"), UiRelations.Child, 0)]
			: ImmutableArray<UiChildLink>.Empty;

		var tree = fixture.Builder.Build(new UiTreeQuery(null, false, UiCaptureOptions.Default));

		AreEqual(1, tree.Nodes.Length);
		AreEqual(1, tree.Warnings.Length);
	}
}
