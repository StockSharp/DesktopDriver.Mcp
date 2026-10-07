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
}
