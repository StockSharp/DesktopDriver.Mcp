namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Styling;
using global::Avalonia.Threading;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Which theme a control is drawn in.
/// </summary>
/// <remarks>
/// A product that remembers the theme a person chose is checked by reading the theme its window comes back in; the
/// colours of a picture would say it only by guessing.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class ThemeTests : BaseTestClass
{
	private static Task RunAsync(Func<Task> body) => HeadlessRun.OnUiThread(body);

	[TestMethod]
	[Timeout(60000)]
	public Task AControlSaysWhichThemeItsWindowIsDrawnIn() => RunAsync(async () =>
	{
		var button = new Button { Name = "Themed", Content = "Themed" };
		var window = new Window { Name = "ThemeWindow", Width = 300, Height = 200, Content = button, RequestedThemeVariant = ThemeVariant.Dark };

		window.Show();
		Dispatcher.UIThread.RunJobs();

		using var fixture = new AutomationFixture(window);
		var id = fixture.Bind(button);

		AreEqual("Dark", await ThemeAsync(fixture, id));

		window.RequestedThemeVariant = ThemeVariant.Light;
		Dispatcher.UIThread.RunJobs();

		AreEqual("Light", await ThemeAsync(fixture, id));
	});

	private static async Task<string> ThemeAsync(AutomationFixture fixture, UiNodeId id)
		=> (await fixture.Snapshots.CaptureAsync(UiTarget.FromId(id), UiCaptureOptions.Default, CancellationToken.None)).Presentation.Theme switch
		{
			UiKnown<string> known => known.Value,
			var other => $"unknown: {other}",
		};
}
