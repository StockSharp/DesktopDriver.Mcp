namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Ecng.UnitTesting;

using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;

/// <summary>
/// What a node is called when several controls carry the same name.
/// </summary>
/// <remarks>
/// A control template names its own parts - PART_ContentPresenter and the like - and every instance of
/// that template carries the same name. Those names belong to the template rather than to the window,
/// so they cannot be identities: two of them would be one address, and a walk that had already passed
/// the first would treat the second as somewhere it had been, and never look inside it.
/// <para>
/// That is not a small mistake. Everything inside the second one - a whole panel, a grid, a chart -
/// disappears from the tree, and a search for it answers honestly that there is no such control.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class NameCollisionTests : BaseTestClass
{
	private static Task RunAsync(Action body) => HeadlessRun.OnUiThread(body);

	private static Window NewWindow()
	{
		// Two templated controls, each of which builds a part its template calls PART_ContentPresenter.
		var first = new ContentControl { Name = "FirstHost", Content = Buried("first.control") };
		var second = new ContentControl { Name = "SecondHost", Content = Buried("second.control") };

		return new Window
		{
			Name = "TwinWindow",
			Width = 400,
			Height = 300,
			Content = new StackPanel { Children = { first, second } },
		};
	}

	// A control built from markup of its own that gives one of its parts the name the window gave the control - the
	// way an instrument picker the panel calls SecuritiesGrid calls its table SecuritiesGrid as well.
	private static Window NestedTwinWindow(out UserControl outer, out ListBox inner)
	{
		inner = new ListBox { Name = "Rows" };
		outer = new UserControl { Name = "Rows", Content = inner };

		return new Window
		{
			Name = "NestedWindow",
			Width = 400,
			Height = 300,
			Content = outer,
		};
	}

	private static void Shown(Window window)
	{
		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame()?.Dispose();
	}

	private static Control Buried(string automationId)
	{
		var button = new Button { Content = automationId };

		AutomationProperties.SetAutomationId(button, automationId);

		return button;
	}

	private static UiTreeSnapshot Walk(AutomationFixture fixture)
		=> fixture.Tree.Build(new UiTreeQuery(
			null,
			false,
			UiCaptureOptions.Default with { Budget = new UiReadBudget(64, 20000, 2000) }));

	[TestMethod]
	[Timeout(60000)]
	public Task BothTwinsAreWalkedRatherThanOnlyTheFirst() => RunAsync(() =>
	{
		var window = NewWindow();

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame()?.Dispose();

		using var fixture = new AutomationFixture(window);

		try
		{
			var ids = Walk(fixture).Nodes.Select(node => node.Node.Id.LocalId).ToArray();

			IsTrue(ids.Contains("first.control"), $"The first one was not reached; the tree holds {string.Join(", ", ids)}.");
			IsTrue(ids.Contains("second.control"), $"The second one was not reached; the tree holds {string.Join(", ", ids)}.");
		}
		finally
		{
			window.Close();
		}
	});

	[TestMethod]
	[Timeout(60000)]
	public Task TwoTemplatePartsWithOneNameAreTwoAddresses() => RunAsync(() =>
	{
		var window = NewWindow();

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame()?.Dispose();

		using var fixture = new AutomationFixture(window);

		try
		{
			var ids = Walk(fixture).Nodes.Select(node => node.Node.Id.LocalId).ToArray();

			AreEqual(ids.Length, ids.Distinct().Count(), $"Two nodes share an address: {string.Join(", ", ids)}.");
		}
		finally
		{
			window.Close();
		}
	});

	[TestMethod]
	[Timeout(60000)]
	public Task ANameTheAuthorWroteIsStillTheAddress() => RunAsync(() =>
	{
		var window = NewWindow();

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame()?.Dispose();

		using var fixture = new AutomationFixture(window);

		try
		{
			var ids = Walk(fixture).Nodes.Select(node => node.Node.Id.LocalId).ToArray();

			// Only the template's own parts lose their names. A name somebody wrote in the window is what
			// the window calls that control, and it stays the address.
			IsTrue(ids.Contains("FirstHost"), string.Join(", ", ids));
			IsTrue(ids.Contains("SecondHost"), string.Join(", ", ids));
		}
		finally
		{
			window.Close();
		}
	});

	/// <summary>
	/// A part of a control's template is addressed through the control it belongs to.
	/// </summary>
	/// <remarks>
	/// The part's own name is not an address, and the position that stood in for it named nothing a step could
	/// write down: the minutes of a time frame editor could only be reached by counting borders and grids.
	/// </remarks>
	[TestMethod]
	[Timeout(60000)]
	public Task ATemplatePartIsAddressedThroughTheControlItBelongsTo() => RunAsync(() =>
	{
		var window = NewWindow();

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame()?.Dispose();

		using var fixture = new AutomationFixture(window);

		try
		{
			var ids = Walk(fixture).Nodes.Select(node => node.Node.Id.LocalId).ToArray();

			IsTrue(ids.Contains("FirstHost.PART_ContentPresenter"), string.Join(", ", ids));
			IsTrue(ids.Contains("SecondHost.PART_ContentPresenter"), string.Join(", ", ids));
		}
		finally
		{
			window.Close();
		}
	});

	/// <summary>
	/// A part named like the control it sits in is addressed through that control.
	/// </summary>
	/// <remarks>
	/// The name the window gave the control stays the control's address; the part's own markup named it too, but
	/// one address cannot mean both.
	/// </remarks>
	[TestMethod]
	[Timeout(60000)]
	public Task APartNamedLikeTheControlItSitsInIsAddressedThroughIt() => RunAsync(() =>
	{
		var window = NestedTwinWindow(out var outer, out var inner);

		Shown(window);

		using var fixture = new AutomationFixture(window);

		try
		{
			AreEqual(new UiNodeId("window:NestedWindow", "Rows"), fixture.Bind(outer));
			AreEqual(new UiNodeId("window:NestedWindow", "Rows.Rows"), fixture.Bind(inner));

			var ids = Walk(fixture).Nodes.Select(node => node.Node.Id.LocalId).ToArray();

			AreEqual(ids.Length, ids.Distinct().Count(), $"Two nodes share an address: {string.Join(", ", ids)}.");
		}
		finally
		{
			window.Close();
		}
	});

	/// <summary>
	/// Which control an address means does not depend on which of the two was read first.
	/// </summary>
	/// <remarks>
	/// One address for both meant whatever had been read last: a table asked for its rows answered from the panel
	/// that holds it, and went on doing so for as long as nothing walked the window again.
	/// </remarks>
	[TestMethod]
	[Timeout(60000)]
	public Task EachOfTheTwoIsFoundByItsAddressWhicheverWasReadFirst() => RunAsync(() =>
	{
		var window = NestedTwinWindow(out var outer, out var inner);

		Shown(window);

		using var fixture = new AutomationFixture(window);

		try
		{
			fixture.Bind(inner);

			AreSame(outer, fixture.Nodes.Resolve(UiTarget.FromId(new UiNodeId("window:NestedWindow", "Rows"))).Instance);

			fixture.Bind(outer);

			AreSame(inner, fixture.Nodes.Resolve(UiTarget.FromId(new UiNodeId("window:NestedWindow", "Rows.Rows"))).Instance);
		}
		finally
		{
			window.Close();
		}
	});
}
