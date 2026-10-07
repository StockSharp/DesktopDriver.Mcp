namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Ecng.UnitTesting;

using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// How far a search goes before it gives up.
/// </summary>
/// <remarks>
/// A search that quietly stops short is worse than a slow one: it answers "there is no such control"
/// about a control that is on screen, and every caller believes it. Real interfaces nest deeply - a
/// window, a tab control, a splitter, a group box, a host, a presenter - and a control twenty levels
/// down is an ordinary control, not an unusual one.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class FindReachTests : BaseTestClass
{
	private const string _buried = "buried.control";
	private const int _depth = 30;

	private static Task RunAsync(Func<Task> body) => HeadlessRun.OnUiThread(body);

	private static Window NewWindow()
	{
		var deep = new Button { Name = "Deep", Content = "Deep" };

		AutomationProperties.SetAutomationId(deep, _buried);

		Control current = deep;

		for (var level = 0; level < _depth; level++)
			current = new Border { Child = current };

		return new Window { Name = "DeepWindow", Width = 400, Height = 300, Content = current };
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

	[TestMethod]
	[Timeout(60000)]
	public Task ASearchReachesAControlThatIsBuriedDeepInTheWindow() => RunAsync(async () =>
	{
		var window = NewWindow();

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame()?.Dispose();

		using var fixture = new AutomationFixture(window);

		try
		{
			var service = NewService(fixture);

			var found = await service.FindAsync(
				new UiFindQuery(
					new UiSelector(null, _buried, null, null, null, null),
					new UiPageRequest(10, null)),
				CancellationToken);

			AreEqual(1, found.Items.Length, $"Nothing was found {_depth} levels down.");
			AreEqual(_buried, found.Items[0].Id.LocalId);
		}
		finally
		{
			window.Close();
		}
	});

	/// <summary>
	/// A box whose caption is drawn - a logo and a name - is found by the name it is given for people who cannot
	/// see it.
	/// </summary>
	/// <remarks>
	/// Such a box has no text of its own, so a search for the name it shows answered that there was no such
	/// control, and a step that picks a source by what it says could not reach the box at all.
	/// </remarks>
	[TestMethod]
	[Timeout(60000)]
	public Task ABoxWithADrawnCaptionIsFoundByItsName() => RunAsync(async () =>
	{
		var box = new CheckBox { Content = new StackPanel { Children = { new Border(), new TextBlock { Text = "ByBit" } } } };
		AutomationProperties.SetName(box, "ByBit");

		var window = new Window { Name = "BoxWindow", Width = 400, Height = 300, Content = box };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame()?.Dispose();

		using var fixture = new AutomationFixture(window);

		try
		{
			var found = await NewService(fixture).FindAsync(
				new UiFindQuery(
					new UiSelector(null, null, null, "toggle", "ByBit", null),
					new UiPageRequest(10, null)),
				CancellationToken);

			AreEqual(1, found.Items.Length, "The box was not found by the name it shows.");
		}
		finally
		{
			window.Close();
		}
	});

	[TestMethod]
	[Timeout(60000)]
	public Task NamingTheSameControlDirectlyAlsoReachesIt() => RunAsync(async () =>
	{
		var window = NewWindow();

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();
		window.CaptureRenderedFrame()?.Dispose();

		using var fixture = new AutomationFixture(window);

		try
		{
			var service = NewService(fixture);

			// The two ways of reaching a node have to agree. A search that stopped short while the
			// address still resolved would make "not found" mean "not found yet".
			var snapshot = await service.CaptureAsync(
				UiTarget.FromId(new UiNodeId("window:DeepWindow", _buried)),
				UiCaptureOptions.Default,
				CancellationToken);

			AreEqual(_buried, snapshot.Node.Id.LocalId);
		}
		finally
		{
			window.Close();
		}
	});
}
