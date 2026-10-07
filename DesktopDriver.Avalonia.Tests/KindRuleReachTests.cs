namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using Ecng.UnitTesting;

using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// Whether the tree mentions a control that a library, rather than Avalonia, says is worth mentioning.
/// </summary>
/// <remarks>
/// The default tree leaves out the scaffolding - borders, presenters, panels - and keeps what a person
/// would point at. A chart is not one of the types Avalonia defines, so without this it counted as
/// scaffolding: it vanished from the tree, and anything asking what a panel was showing was told
/// nothing, which reads exactly like a panel with nothing in it.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class KindRuleReachTests : BaseTestClass
{
	// Neither named nor one of the types Avalonia defines - only the rule makes it worth mentioning.
	private sealed class Painting : Control;

	[TestMethod]
	[Timeout(60000)]
	public Task AControlALibraryNamedAKindOfItsOwnIsInTheTree() => HeadlessRun.OnUiThread(async () =>
	{
		var unnamed = await KindsAsync(false);
		var named = await KindsAsync(true);

		IsFalse(unnamed.Contains("chart"), "Nothing said this control was a chart, and the tree called it one.");

		IsTrue(
			named.Contains("chart"),
			"The tree leaves out a control the library named a kind of its own: " + string.Join(", ", named));

		// One node more, and it is that one: the rule must not change what the tree says about anything
		// else in the window.
		AreEqual(unnamed.Count + 1, named.Count);
	});

	private async Task<IReadOnlyList<string>> KindsAsync(bool ruled)
	{
		var window = new Window
		{
			Name = "KindWindow",
			Width = 400,
			Height = 300,
			Content = new Border { Child = new Border { Child = new Painting() } },
		};

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame()?.Dispose();

		using var fixture = new AutomationFixture(window);

		try
		{
			if (ruled)
				fixture.Binder.KindRules.Add(control => control is Painting ? "chart" : null);

			var root = fixture.Bind(window);

			var tree = await NewService(fixture).GetTreeAsync(
				new UiTreeQuery(UiTarget.FromId(root), false, UiCaptureOptions.Default),
				CancellationToken);

			return [.. tree.Nodes.Select(node => node.Node.Kind)];
		}
		finally
		{
			window.Close();
		}
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
}
