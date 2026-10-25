namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Presenters;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Media.TextFormatting;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Avalonia;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// Pictures, and what a picture is evidence of.
/// </summary>
[TestClass]
[DoNotParallelize]
public class ScreenshotTests : BaseTestClass
{
	private static Task RunAsync(Func<Task> body) => HeadlessRun.OnUiThread(body);

	[TestMethod]
	[Timeout(60000)]
	public Task WhatIsMaskedIsNotInThePicture() => RunAsync(async () =>
	{
		// A window that names whoever is signed in can be pictured without picturing them - and the point
		// is that the picture of them never exists, rather than being taken and cut out afterwards.
		var secret = new Border
		{
			Name = "TheSecret",
			Width = 200,
			Height = 40,
			Background = Brushes.White,
			Child = new TextBlock
			{
				Text = "SECRET",
				FontSize = 28,
				FontWeight = FontWeight.Bold,
				Foreground = Brushes.Black,
				Margin = new(8, 2),
			},
		};

		var page = new StackPanel { Name = "ThePage", Width = 200, Height = 80, Background = Brushes.White };

		page.Children.Add(secret);

		var window = new Window { Name = "MaskWindow", Width = 400, Height = 300, Content = page };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		using var fixture = new AutomationFixture(window);
		var pageId = fixture.Bind(page);
		var secretId = fixture.Bind(secret);
		var artifacts = new UiArtifactStore();

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, artifacts, fixture.InstanceId);

		var plain = await Pixels(service, artifacts, pageId, [], []);
		var masked = await Pixels(service, artifacts, pageId, [new UiMask(secretId, null, UiMaskScopes.Node, null)], []);

		var covered = new PixelRect(0, 0, 200, 40);

		IsTrue(plain.Steepest(covered) > 128, "There were no letters to begin with, so this proves nothing.");

		// Where the letters were drawn edge against edge, neighbouring pixels now differ by a shade at most.
		IsTrue(masked.Steepest(covered) < 24, "What was masked can still be read.");

		// Blurred rather than painted over: the place still has the colour of what was there, so the
		// picture reads as a caption kept out of it rather than a hole cut in the window.
		IsTrue(plain.Mean(covered).Distance(masked.Mean(covered)) < 16, "What was masked was painted over rather than blurred.");

		// And what was not masked is untouched: the page is taller than the control it covers.
		IsTrue(plain.Same(masked, new(0, 44, 200, 36)), "Masking changed something it was not asked to.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task PrivateTextIsKeptOutWhereverItIsDrawn() => RunAsync(async () =>
	{
		// The name of whoever is signed in is shown on its own, and quoted again in the middle of a line of
		// the log, where no control of its own holds it for a mask to name.
		var user = Line("UserName", "Gooner");
		var log = Line("LogLine", "name = 'Gooner', eula accepted");
		var other = Line("OtherLine", "Nothing to hide here");

		var page = new StackPanel { Name = "LogPage", Width = 380, Spacing = 12, Background = Brushes.White };

		page.Children.Add(user);
		page.Children.Add(log);
		page.Children.Add(other);

		var window = new Window { Name = "PrivateWindow", Width = 400, Height = 300, Content = page };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		using var fixture = new AutomationFixture(window);
		var pageId = fixture.Bind(page);
		var userId = fixture.Bind(user);
		var artifacts = new UiArtifactStore();

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, artifacts, fixture.InstanceId);

		var plain = await Pixels(service, artifacts, pageId, [], []);
		var masked = await Pixels(service, artifacts, pageId, [new UiMask(userId, null, UiMaskScopes.Text, null)], []);

		var shown = Drawn.Where(user, page, "Gooner");
		var quoted = Drawn.Where(log, page, "Gooner");

		IsTrue(plain.Steepest(quoted) > 128, "The name was not drawn in the log line, so this proves nothing.");

		IsTrue(masked.Steepest(shown) < 24, "The name can still be read where it is shown on its own.");
		IsTrue(masked.Steepest(quoted) < 24, "The name can still be read in the middle of the log line.");

		// The rest of the line is what the picture is for.
		IsTrue(plain.Same(masked, Drawn.Where(log, page, "name =")), "The line was blurred before the name.");
		IsTrue(plain.Same(masked, Drawn.Where(log, page, "eula accepted")), "The line was blurred after the name.");
		IsTrue(plain.Same(masked, Drawn.Where(other, page, other.Text)), "A line that does not carry the name was touched.");

		static TextBlock Line(string name, string text)
			=> new() { Name = name, Text = text, FontSize = 20, Foreground = Brushes.Black };
	});

	[TestMethod]
	[Timeout(60000)]
	public Task PrivateTextThatIsNotKnownYetIsRefused() => RunAsync(async () =>
	{
		// A status bar still waiting for the sign-in names nobody, and a picture taken then would carry the
		// name wherever else it is already drawn, with nothing to say what to look for.
		var user = new TextBlock { Name = "UserName", Text = string.Empty };
		var page = new StackPanel { Name = "StatusPage", Width = 200, Height = 60, Background = Brushes.White };

		page.Children.Add(user);
		page.Children.Add(new TextBlock { Text = "name = 'Gooner'" });

		var window = new Window { Name = "UnknownWindow", Width = 400, Height = 300, Content = page };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		using var fixture = new AutomationFixture(window);
		var pageId = fixture.Bind(page);
		var userId = fixture.Bind(user);

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, new UiArtifactStore(), fixture.InstanceId);

		var error = await ThrowsAsync<UiAutomationException>(() => service.CaptureAsync(
			new UiScreenshotRequest(
				UiTarget.FromId(pageId),
				UiCaptureKinds.ControlRender,
				false,
				0,
				0,
				null,
				[new UiMask(userId, null, UiMaskScopes.Text, null)],
				[]),
			CancellationToken.None));

		AreEqual(UiErrorCodes.NotCreated, error.Error.Code);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task LocalPathsAreKeptOutWhereverTheyAreDrawn() => RunAsync(async () =>
	{
		// Where a product keeps its data is a folder of the machine the picture was taken on: shown on its own,
		// quoted in a line of the log, edited in a box. A path may hold a space, so where it ends cannot be told.
		var storage = Line("StorageLine", @"D:\work\Storage");
		var log = Line("LogLine", @"Loaded from D:\my work\Storage 5 files");
		var home = Line("HomeLine", "Read /home/somebody/settings.json");
		var share = Line("ShareLine", @"Copied to \\server\share\x");
		var other = Line("OtherLine", "At 10:30 from https://stocksharp.com");
		var box = new TextBox { Name = "PathBox", Text = @"C:\Users\somebody\Documents", FontSize = 20 };

		var page = new StackPanel { Name = "PathPage", Width = 380, Spacing = 12, Background = Brushes.White };

		page.Children.Add(storage);
		page.Children.Add(log);
		page.Children.Add(home);
		page.Children.Add(share);
		page.Children.Add(other);
		page.Children.Add(box);

		var window = new Window { Name = "PathWindow", Width = 400, Height = 400, Content = page };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		using var fixture = new AutomationFixture(window);
		var pageId = fixture.Bind(page);
		var artifacts = new UiArtifactStore();

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, artifacts, fixture.InstanceId);

		var plain = await Pixels(service, artifacts, pageId, [], []);
		var masked = await Pixels(service, artifacts, pageId, [new UiMask(pageId, null, UiMaskScopes.Paths, null)], []);

		var quoted = Drawn.Where(log, page, @"D:\my work\Storage 5 files");
		var typed = Drawn.Where(box.GetVisualDescendants().OfType<TextPresenter>().Single(), page, box.Text);

		IsTrue(plain.Steepest(quoted) > 128, "The path was not drawn in the log line, so this proves nothing.");
		IsTrue(plain.Steepest(typed) > 128, "The path was not drawn in the box, so this proves nothing.");

		IsTrue(masked.Steepest(Drawn.Where(storage, page, storage.Text)) < 24, "A path shown on its own can still be read.");
		IsTrue(masked.Steepest(quoted) < 24, "A path can still be read past its space, in the middle of the log line.");
		IsTrue(masked.Steepest(Drawn.Where(home, page, "/home/somebody/settings.json")) < 24, "A home folder can still be read.");
		IsTrue(masked.Steepest(Drawn.Where(share, page, @"\\server\share\x")) < 24, "A share can still be read.");
		IsTrue(masked.Steepest(typed) < 24, "A path being edited can still be read.");

		// The words around a path are what the picture is for, and a time or an address is not a path.
		IsTrue(plain.Same(masked, Drawn.Where(log, page, "Loaded from")), "The line was blurred before the path.");
		IsTrue(plain.Same(masked, Drawn.Where(home, page, "Read")), "The line was blurred before the home folder.");
		IsTrue(plain.Same(masked, Drawn.Where(other, page, other.Text)), "A line with no path in it was touched.");

		static TextBlock Line(string name, string text)
			=> new() { Name = name, Text = text, FontSize = 20, Foreground = Brushes.Black };
	});

	[TestMethod]
	[Timeout(60000)]
	public Task APathCutShortIsKeptOutAsFarAsItIsDrawn() => RunAsync(async () =>
	{
		// A storage too long for its box is drawn up to an ellipsis, and what is drawn of it is the beginning of
		// the folder - the very part that says whose machine it is.
		var cut = new TextBlock
		{
			Name = "CutLine",
			Text = @"D:\somebody\work\stocksharp\Storage",
			Width = 120,
			TextTrimming = TextTrimming.CharacterEllipsis,
			FontSize = 20,
			Foreground = Brushes.Black,
		};

		var page = new StackPanel { Name = "CutPage", Width = 200, Height = 60, Background = Brushes.White };

		page.Children.Add(cut);

		var window = new Window { Name = "CutWindow", Width = 400, Height = 300, Content = page };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		using var fixture = new AutomationFixture(window);
		var pageId = fixture.Bind(page);
		var artifacts = new UiArtifactStore();

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, artifacts, fixture.InstanceId);

		var plain = await Pixels(service, artifacts, pageId, [], []);
		var masked = await Pixels(service, artifacts, pageId, [new UiMask(pageId, null, UiMaskScopes.Paths, null)], []);

		var drawn = Drawn.Bounds(cut, page);

		IsTrue(plain.Steepest(drawn) > 128, "Nothing of the path was drawn, so this proves nothing.");
		IsTrue(masked.Steepest(drawn) < 24, "What is drawn of the path can still be read.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task PathsAreKeptOutOnlyInsideTheNodeNamed() => RunAsync(async () =>
	{
		// A product names the panel whose paths are its own business; a path drawn beside that panel is somebody
		// else's decision.
		var inside = new TextBlock { Name = "InsideLine", Text = @"D:\inside\Storage", FontSize = 20, Foreground = Brushes.Black };
		var outside = new TextBlock { Name = "OutsideLine", Text = @"D:\outside\Storage", FontSize = 20, Foreground = Brushes.Black };
		var panel = new StackPanel { Name = "InsidePanel" };

		panel.Children.Add(inside);

		var page = new StackPanel { Name = "BothPage", Width = 380, Spacing = 12, Background = Brushes.White };

		page.Children.Add(panel);
		page.Children.Add(outside);

		var window = new Window { Name = "BothWindow", Width = 400, Height = 300, Content = page };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		using var fixture = new AutomationFixture(window);
		var pageId = fixture.Bind(page);
		var panelId = fixture.Bind(panel);
		var artifacts = new UiArtifactStore();

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, artifacts, fixture.InstanceId);

		var plain = await Pixels(service, artifacts, pageId, [], []);
		var masked = await Pixels(service, artifacts, pageId, [new UiMask(panelId, null, UiMaskScopes.Paths, null)], []);

		IsTrue(masked.Steepest(Drawn.Where(inside, page, inside.Text)) < 24, "The path inside the node can still be read.");
		IsTrue(plain.Same(masked, Drawn.Where(outside, page, outside.Text)), "A path outside the node was touched.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task APartOfThePathsIsRefused() => RunAsync(async () =>
	{
		var line = new TextBlock { Name = "PartLine", Text = @"D:\work\Storage" };
		var window = new Window { Name = "PartWindow", Width = 400, Height = 300, Content = line };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		using var fixture = new AutomationFixture(window);
		var id = fixture.Bind(line);

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, new UiArtifactStore(), fixture.InstanceId);

		// The paths are found wherever they are drawn inside the node, so a column of it narrows nothing.
		var error = await ThrowsAsync<UiAutomationException>(() => service.CaptureAsync(
			new UiScreenshotRequest(
				UiTarget.FromId(id),
				UiCaptureKinds.ControlRender,
				false,
				0,
				0,
				null,
				[new UiMask(id, new UiGridHeaderPart("Path"), UiMaskScopes.Paths, null)],
				[]),
			CancellationToken.None));

		AreEqual(UiErrorCodes.InvalidRequest, error.Error.Code);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task WhatAPatternMatchesIsKeptOutAndNothingElse() => RunAsync(async () =>
	{
		// A product writes the number of a licence into a line of its log on a page where no control of its own holds
		// that number, so its shape is all there is to find it by; the rest of the line is what the picture is for.
		var saved = Line("SavedLine", "saving license (id=108839)...");
		var other = Line("OtherLine", "license request complete");
		var outside = Line("OutsideLine", "saving license (id=108840)...");
		var log = new StackPanel { Name = "LogPanel", Spacing = 12 };

		log.Children.Add(saved);
		log.Children.Add(other);

		var page = new StackPanel { Name = "PatternPage", Width = 380, Spacing = 12, Background = Brushes.White };

		page.Children.Add(log);
		page.Children.Add(outside);

		var window = new Window { Name = "PatternWindow", Width = 400, Height = 300, Content = page };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		using var fixture = new AutomationFixture(window);
		var pageId = fixture.Bind(page);
		var logId = fixture.Bind(log);
		var artifacts = new UiArtifactStore();

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, artifacts, fixture.InstanceId);

		var plain = await Pixels(service, artifacts, pageId, [], []);
		var masked = await Pixels(service, artifacts, pageId, [new UiMask(logId, null, UiMaskScopes.Pattern, @"(?<=\(id=)\d+")], []);

		var number = Drawn.Where(saved, page, "108839");

		IsTrue(plain.Steepest(number) > 128, "The number was not drawn, so this proves nothing.");
		IsTrue(masked.Steepest(number) < 24, "The number can still be read.");

		IsTrue(plain.Same(masked, Drawn.Where(saved, page, "saving license")), "The words before the number were touched.");
		IsTrue(plain.Same(masked, Drawn.Where(other, page, other.Text)), "A line the pattern does not match was touched.");
		IsTrue(plain.Same(masked, Drawn.Where(outside, page, outside.Text)), "A match outside the node was touched.");

		static TextBlock Line(string name, string text)
			=> new() { Name = name, Text = text, FontSize = 20, Foreground = Brushes.Black };
	});

	[TestMethod]
	[Timeout(60000)]
	public Task APartOfWhatAPatternMatchesIsRefused() => RunAsync(async () =>
	{
		var error = await PatternRefusalAsync("PatternPartWindow", new UiGridHeaderPart("Message"), @"\d+");

		// The matches are found wherever they are drawn inside the node, so a column of it narrows nothing.
		AreEqual(UiErrorCodes.InvalidRequest, error.Error.Code);
	});

	[TestMethod]
	[Timeout(60000)]
	[DataRow("PatternMissingWindow", null)]
	[DataRow("PatternEmptyWindow", "")]
	[DataRow("PatternBrokenWindow", "(id=")]
	public Task APatternThatCannotBeReadIsRefused(string windowName, string pattern) => RunAsync(async () =>
	{
		// Taking the picture without the mask would publish what the mask was asked to keep out.
		var error = await PatternRefusalAsync(windowName, null, pattern);

		AreEqual(UiErrorCodes.InvalidRequest, error.Error.Code);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task MaskingSomethingThatIsNotThereIsRefused() => RunAsync(async () =>
	{
		var button = new Button { Name = "TheButton", Content = "Press me", Width = 120, Height = 40 };
		var window = new Window { Name = "MissingMaskWindow", Width = 400, Height = 300, Content = button };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		using var fixture = new AutomationFixture(window);
		var id = fixture.Bind(button);
		var artifacts = new UiArtifactStore();

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, artifacts, fixture.InstanceId);

		// Quietly taking the picture anyway is how a name nobody meant to publish gets published.
		await ThrowsAsync<UiAutomationException>(() => service.CaptureAsync(
			new UiScreenshotRequest(
				UiTarget.FromId(id),
				UiCaptureKinds.ControlRender,
				false,
				0,
				0,
				null,
				[new UiMask(new UiNodeId("window:MissingMaskWindow", "NoSuchThing"), null, UiMaskScopes.Node, null)],
				[]),
			CancellationToken.None));
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AHighlightIsAFrameRoundWhatItPointsAt() => RunAsync(async () =>
	{
		// A page about one command of a ribbon shows the whole ribbon with that command framed: the frame runs round
		// the command a little outside it, and what the command draws is left as it was.
		var (window, ribbon, command) = Ribbon("FramedWindow");

		using var fixture = new AutomationFixture(window);
		var ribbonId = fixture.Bind(ribbon);
		var commandId = fixture.Bind(command);
		var artifacts = new UiArtifactStore();

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, artifacts, fixture.InstanceId);

		var plain = await Pixels(service, artifacts, ribbonId, [], []);
		var framed = await Pixels(service, artifacts, ribbonId, [], [new UiHighlight(commandId, null)]);

		var scaling = window.RenderScaling;
		var left = (int)(110 * scaling);
		var top = (int)(40 * scaling);
		var right = (int)(190 * scaling);
		var bottom = (int)(80 * scaling);

		IsTrue(plain.Same(framed, new PixelRect(left, top, right - left, bottom - top)), "The frame covers what it points at.");

		// Five units outside every side: a frame stands off what it points at rather than touching it.
		foreach (var (x, y) in new[] { (left - (int)(5 * scaling), (top + bottom) / 2), (right + (int)(4 * scaling), (top + bottom) / 2), ((left + right) / 2, top - (int)(5 * scaling)), ((left + right) / 2, bottom + (int)(4 * scaling)) })
		{
			var drawn = framed.At(x, y);

			IsTrue(drawn.R > 180 && drawn.G < 120 && drawn.B < 120, $"No frame at {x}, {y}: {drawn}.");
		}

		IsTrue(plain.Same(framed, new PixelRect(0, 0, left - (int)(12 * scaling), (int)(120 * scaling))), "The frame reaches far beyond what it points at.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AHighlightOfSomethingNotThereIsRefused() => RunAsync(async () =>
	{
		var (window, ribbon, _) = Ribbon("MissingFrameWindow");

		using var fixture = new AutomationFixture(window);
		var ribbonId = fixture.Bind(ribbon);

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, new UiArtifactStore(), fixture.InstanceId);

		// A picture whose frame went missing points at nothing, and the page that explains the framed command
		// would be explaining a picture without it.
		await ThrowsAsync<UiAutomationException>(() => service.CaptureAsync(
			new UiScreenshotRequest(
				UiTarget.FromId(ribbonId),
				UiCaptureKinds.ControlRender,
				false,
				0,
				0,
				null,
				[],
				[new UiHighlight(new UiNodeId("window:MissingFrameWindow", "NoSuchThing"), null)]),
			CancellationToken.None));
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AHighlightOutsideThePictureIsRefused() => RunAsync(async () =>
	{
		var (window, ribbon, command) = Ribbon("OutsideFrameWindow");

		using var fixture = new AutomationFixture(window);
		var ribbonId = fixture.Bind(ribbon);
		var commandId = fixture.Bind(command);

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, new UiArtifactStore(), fixture.InstanceId);

		// The ribbon is round the command, not the other way round: a picture of the command alone cannot frame the
		// ribbon it stands in.
		var error = await ThrowsAsync<UiAutomationException>(() => service.CaptureAsync(
			new UiScreenshotRequest(
				UiTarget.FromId(commandId),
				UiCaptureKinds.ControlRender,
				false,
				0,
				0,
				null,
				[],
				[new UiHighlight(ribbonId, null)]),
			CancellationToken.None));

		AreEqual(UiErrorCodes.InvalidRequest, error.Error.Code);
	});

	// A white ribbon with one grey command on it, 80 by 40 at 110, 40.
	// A line with a licence number in it, pictured with the one mask of a pattern the caller gives.
	private async Task<UiAutomationException> PatternRefusalAsync(string windowName, UiTargetPart part, string pattern)
	{
		var line = new TextBlock { Name = "PatternLine", Text = "saving license (id=108839)..." };
		var window = new Window { Name = windowName, Width = 400, Height = 300, Content = line };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		using var fixture = new AutomationFixture(window);
		var id = fixture.Bind(line);

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, new UiArtifactStore(), fixture.InstanceId);

		return await ThrowsAsync<UiAutomationException>(() => service.CaptureAsync(
			new UiScreenshotRequest(
				UiTarget.FromId(id),
				UiCaptureKinds.ControlRender,
				false,
				0,
				0,
				null,
				[new UiMask(id, part, UiMaskScopes.Pattern, pattern)],
				[]),
			CancellationToken.None));
	}

	private static (Window Window, Canvas Ribbon, Border Command) Ribbon(string name)
	{
		var command = new Border { Name = "TheCommand", Width = 80, Height = 40, Background = Brushes.LightGray };
		var ribbon = new Canvas { Name = "TheRibbon", Width = 300, Height = 120, Background = Brushes.White };

		Canvas.SetLeft(command, 110);
		Canvas.SetTop(command, 40);
		ribbon.Children.Add(command);

		var window = new Window { Name = name, Width = 400, Height = 300, Content = ribbon };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		return (window, ribbon, command);
	}

	// Decoded rather than compared as bytes: a PNG of a flat rectangle compresses differently, so the
	// length of the file says nothing about what is in the picture.
	private static async Task<Drawn> Pixels(
		AvaloniaScreenshotService service,
		UiArtifactStore artifacts,
		UiNodeId target,
		ImmutableArray<UiMask> mask,
		ImmutableArray<UiHighlight> highlight)
	{
		var info = await service.CaptureAsync(
			new UiScreenshotRequest(UiTarget.FromId(target), UiCaptureKinds.ControlRender, false, 0, 0, null, mask, highlight),
			CancellationToken.None);

		var data = artifacts.Read(new UiArtifactReadRequest(info.ArtifactId, 0, 4 * 1024 * 1024)).Data.ToArray();

		using var stream = new MemoryStream(data);
		using var bitmap = new Bitmap(stream);

		var size = bitmap.PixelSize;
		var pixels = new byte[size.Width * size.Height * 4];

		var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);

		try
		{
			bitmap.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), pixels.Length, size.Width * 4);
		}
		finally
		{
			handle.Free();
		}

		return new(pixels, size.Width);
	}

	private readonly record struct Drawn(byte[] Pixels, int Width)
	{
		public Color At(int x, int y)
		{
			var at = (y * Width + x) * 4;

			// The frame comes back as blue, green, red, alpha.
			return Color.FromArgb(Pixels[at + 3], Pixels[at + 2], Pixels[at + 1], Pixels[at]);
		}

		// The largest step in any colour between two pixels side by side or one above the other: the edge of
		// a letter is a step of most of the range, and a blur leaves none.
		public int Steepest(PixelRect area)
		{
			var steepest = 0;

			for (var y = area.Y; y < area.Bottom; y++)
			{
				for (var x = area.X; x < area.Right; x++)
				{
					if (x + 1 < area.Right)
						steepest = Math.Max(steepest, Step(x, y, x + 1, y));

					if (y + 1 < area.Bottom)
						steepest = Math.Max(steepest, Step(x, y, x, y + 1));
				}
			}

			return steepest;
		}

		public Shade Mean(PixelRect area)
		{
			double first = 0, second = 0, third = 0;

			for (var y = area.Y; y < area.Bottom; y++)
			{
				for (var x = area.X; x < area.Right; x++)
				{
					var at = (y * Width + x) * 4;

					first += Pixels[at];
					second += Pixels[at + 1];
					third += Pixels[at + 2];
				}
			}

			var count = (double)area.Width * area.Height;

			return new(first / count, second / count, third / count);
		}

		public bool Same(Drawn other, PixelRect area)
		{
			for (var y = area.Y; y < area.Bottom; y++)
			{
				var at = (y * Width + area.X) * 4;

				if (!Pixels.AsSpan(at, area.Width * 4).SequenceEqual(other.Pixels.AsSpan(at, area.Width * 4)))
					return false;
			}

			return true;
		}

		// Where a piece of a line is drawn on the page, in the page's pixels.
		public static PixelRect Where(TextBlock line, Visual page, string text)
			=> Where(line, line.Text, line.TextLayout, page, text);

		// Where a piece of the text a box is editing is drawn on the page, in the page's pixels.
		public static PixelRect Where(TextPresenter presenter, Visual page, string text)
			=> Where(presenter, presenter.Text, presenter.TextLayout, page, text);

		// Where a control is on the page, in the page's pixels.
		public static PixelRect Bounds(Control control, Visual page)
		{
			var origin = control.TranslatePoint(default, page) ?? throw new InvalidOperationException("The control is not on the page.");

			return InPixels(new Rect(origin, control.Bounds.Size), page);
		}

		private static PixelRect Where(Visual owner, string shown, TextLayout layout, Visual page, string text)
		{
			var start = shown.IndexOf(text, StringComparison.Ordinal);

			if (start < 0)
				throw new ArgumentException($"'{shown}' does not hold '{text}'.", nameof(text));

			var origin = owner.TranslatePoint(default, page) ?? throw new InvalidOperationException("The line is not on the page.");
			var drawn = layout.HitTestTextRange(start, text.Length).Aggregate((left, right) => left.Union(right));

			return InPixels(drawn.Translate(origin), page);
		}

		private static PixelRect InPixels(Rect area, Visual page)
		{
			var scaling = TopLevel.GetTopLevel(page)?.RenderScaling ?? 1;

			return new(
				(int)Math.Floor(area.X * scaling),
				(int)Math.Floor(area.Y * scaling),
				(int)Math.Ceiling(area.Width * scaling),
				(int)Math.Ceiling(area.Height * scaling));
		}

		private int Step(int x, int y, int toX, int toY)
		{
			var at = (y * Width + x) * 4;
			var to = (toY * Width + toX) * 4;
			var step = 0;

			for (var channel = 0; channel < 3; channel++)
				step = Math.Max(step, Math.Abs(Pixels[at + channel] - Pixels[to + channel]));

			return step;
		}
	}

	private readonly record struct Shade(double First, double Second, double Third)
	{
		public double Distance(Shade other)
			=> Math.Max(Math.Abs(First - other.First), Math.Max(Math.Abs(Second - other.Second), Math.Abs(Third - other.Third)));
	}

	[TestMethod]
	[Timeout(60000)]
	public Task AControlDrawsItselfIntoARealPng() => RunAsync(async () =>
	{
		var button = new Button { Name = "TheButton", Content = "Press me", Width = 120, Height = 40 };
		var window = new Window { Name = "TestWindow", Width = 400, Height = 300, Content = button };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		using var fixture = new AutomationFixture(window);
		var id = fixture.Bind(button);
		var artifacts = new UiArtifactStore();

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, artifacts, fixture.InstanceId);

		var info = await service.CaptureAsync(
			new UiScreenshotRequest(UiTarget.FromId(id), UiCaptureKinds.ControlRender, false, 0, 0, null, [], []),
			CancellationToken.None);

		AreEqual("image/png", info.MimeType);
		AreEqual(UiCaptureKinds.ControlRender, info.CaptureKind);
		AreEqual(120, info.PixelWidth);
		AreEqual(40, info.PixelHeight);

		var chunk = artifacts.Read(new UiArtifactReadRequest(info.ArtifactId, 0, 64 * 1024));

		IsTrue(chunk.IsLast);
		AreEqual(info.ByteLength, chunk.TotalBytes);

		// A real PNG, not a promise of one: the signature is the file's own claim about itself.
		byte[] signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

		for (var index = 0; index < signature.Length; index++)
			AreEqual(signature[index], chunk.Data[index]);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task AskingForAPhotographOfTheScreenIsRefusedRatherThanAnsweredWithADrawing() => RunAsync(async () =>
	{
		// The two are different evidence. Quietly returning one for the other would let a test claim the
		// user could see something that another window was covering.
		var button = new Button { Name = "TheButton", Content = "Press me", Width = 120, Height = 40 };
		var window = new Window { Name = "TestWindow", Width = 400, Height = 300, Content = button };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		using var fixture = new AutomationFixture(window);
		var id = fixture.Bind(button);

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, new UiArtifactStore(), fixture.InstanceId);

		var error = await ThrowsAsync<UiAutomationException>(() => service.CaptureAsync(
			new UiScreenshotRequest(UiTarget.FromId(id), UiCaptureKinds.ScreenCapture, false, 0, 0, null, [], []),
			CancellationToken.None));

		AreEqual(UiErrorCodes.UnsupportedCapability, error.Error.Code);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task APanelTakingItsGroundFromTheWindowIsNotDrawnOnNothing() => RunAsync(async () =>
	{
		// A workspace sets no background of its own - the window paints it. Drawn on its own it comes out
		// transparent, and the documentation then shows pale text on whatever the reader's viewer happens
		// to put behind a picture.
		var workspace = new Grid { Name = "TheWorkspace", Width = 160, Height = 80 };

		workspace.Children.Add(new TextBlock { Text = "Realtime" });

		var window = new Window
		{
			Name = "TestWindow",
			Width = 400,
			Height = 300,
			Background = Brushes.DarkSlateBlue,
			Content = workspace,
		};

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		using var fixture = new AutomationFixture(window);
		var id = fixture.Bind(workspace);
		var artifacts = new UiArtifactStore();

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, artifacts, fixture.InstanceId);

		var info = await service.CaptureAsync(
			new UiScreenshotRequest(UiTarget.FromId(id), UiCaptureKinds.ControlRender, false, 0, 0, null, [], []),
			CancellationToken.None);

		var chunk = artifacts.Read(new UiArtifactReadRequest(info.ArtifactId, 0, 4 * 1024 * 1024));

		IsTrue(chunk.IsLast);

		// Away from the text, where only the ground is showing.
		var corner = PixelAt(chunk.Data, info.PixelWidth - 2, info.PixelHeight - 2);

		AreEqual(255, corner[3]);

		// DarkSlateBlue, whichever order the decoded picture puts its channels in.
		byte[] channels = [corner[0], corner[1], corner[2]];

		Array.Sort(channels);

		AreEqual(61, channels[0]);
		AreEqual(72, channels[1]);
		AreEqual(139, channels[2]);
	});







	[TestMethod]
	[Timeout(60000)]
	public Task ThePictureIsOfTheControlAskedForRatherThanTheCornerOfTheWindow() => RunAsync(async () =>
	{
		var wanted = new Border { Name = "TheRightHalf", Background = Brushes.ForestGreen };
		var panel = new Grid { ColumnDefinitions = new("*,*") };

		panel.Children.Add(new Border { Background = Brushes.Firebrick });
		panel.Children.Add(wanted);
		Grid.SetColumn(wanted, 1);

		var window = new Window { Name = "TestWindow", Width = 400, Height = 300, Content = panel };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		using var fixture = new AutomationFixture(window);
		var id = fixture.Bind(wanted);
		var artifacts = new UiArtifactStore();

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, artifacts, fixture.InstanceId);

		var info = await service.CaptureAsync(
			new UiScreenshotRequest(UiTarget.FromId(id), UiCaptureKinds.ControlRender, false, 0, 0, null, [], []),
			CancellationToken.None);

		var chunk = artifacts.Read(new UiArtifactReadRequest(info.ArtifactId, 0, 4 * 1024 * 1024));
		var middle = PixelAt(chunk.Data, info.PixelWidth / 2, info.PixelHeight / 2);

		// ForestGreen, whichever order the decoded picture puts its channels in.
		byte[] channels = [middle[0], middle[1], middle[2]];

		Array.Sort(channels);

		AreEqual(34, channels[0]);
		AreEqual(34, channels[1]);
		AreEqual(139, channels[2]);
	});

	[TestMethod]
	[Timeout(60000)]
	public Task TheTextABoxShowsOfItsChoiceIsKeptOutWhereverItIsDrawn() => RunAsync(async () =>
	{
		// An account is chosen in a box on each of several tickets, and the same account is quoted in a line: the box is
		// what a picture names, and what it shows is drawn by a template rather than held as a text of its own.
		var account = new ComboBox
		{
			Name = "Account",
			Width = 200,
			FontSize = 20,
			ItemsSource = new[] { "Gooner" },
			SelectedIndex = 0,
		};
		var line = new TextBlock { Name = "Quote", Text = "account Gooner", FontSize = 20, Foreground = Brushes.Black };

		var page = new StackPanel { Name = "TicketPage", Width = 380, Spacing = 12, Background = Brushes.White };

		page.Children.Add(account);
		page.Children.Add(line);

		var window = new Window { Name = "TicketWindow", Width = 400, Height = 300, Content = page };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		using var fixture = new AutomationFixture(window);
		var pageId = fixture.Bind(page);
		var accountId = fixture.Bind(account);
		var artifacts = new UiArtifactStore();

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, artifacts, fixture.InstanceId);

		var plain = await Pixels(service, artifacts, pageId, [], []);
		var masked = await Pixels(service, artifacts, pageId, [new UiMask(accountId, null, UiMaskScopes.Text, null)], []);

		var quoted = Drawn.Where(line, page, "Gooner");

		IsTrue(plain.Steepest(quoted) > 128, "The account was not drawn in the line, so this proves nothing.");
		IsTrue(masked.Steepest(quoted) < 24, "The account can still be read in the line.");
		IsTrue(plain.Same(masked, Drawn.Where(line, page, "account")), "The line was blurred before the account.");
	});

	[TestMethod]
	[Timeout(60000)]
	public Task APictureWithItsPopupsShowsTheListOpenOverTheControl() => RunAsync(async () =>
	{
		// A page about a choice shows the list it is made from, and the list is a window of its own that hangs below
		// the control it opened from: drawn without it, the picture shows a closed box.
		var combo = new ComboBox
		{
			Name = "TheChoice",
			Width = 160,
			ItemsSource = new[] { "First", "Second", "Third", "Fourth", "Fifth" },
			SelectedIndex = 0,
		};
		var panel = new StackPanel { Name = "ThePanel", Width = 200, Height = 40, VerticalAlignment = VerticalAlignment.Top };

		panel.Children.Add(combo);

		var window = new Window { Name = "TestWindow", Width = 400, Height = 300, Content = panel };

		window.Show();
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		combo.IsDropDownOpen = true;
		Dispatcher.UIThread.RunJobs();
		window.UpdateLayout();

		using var fixture = new AutomationFixture(window);
		var id = fixture.Bind(panel);
		var artifacts = new UiArtifactStore();

		var service = new AvaloniaScreenshotService(
			fixture.Nodes, fixture.Adapters, fixture.Revisions, fixture.Executor, artifacts, fixture.InstanceId);

		var without = await service.CaptureAsync(
			new UiScreenshotRequest(UiTarget.FromId(id), UiCaptureKinds.ControlRender, false, 0, 0, null, [], []),
			CancellationToken.None);

		AreEqual(40, without.PixelHeight, "Without its popups the picture is not of the panel alone.");

		var with = await service.CaptureAsync(
			new UiScreenshotRequest(UiTarget.FromId(id), UiCaptureKinds.ControlRender, true, 0, 0, null, [], []),
			CancellationToken.None);

		IsTrue(with.PixelHeight > 80, $"The open list is not in the picture: it is {with.PixelHeight} tall.");

		var chunk = artifacts.Read(new UiArtifactReadRequest(with.ArtifactId, 0, 4 * 1024 * 1024));

		// Below the panel, where only the list can be.
		AreEqual(255, PixelAt(chunk.Data, 20, with.PixelHeight - 10)[3], "Below the panel the picture is empty.");

		// Below the panel and beside the list the window shows what the picture is not about: it is left out.
		AreEqual(0, PixelAt(chunk.Data, with.PixelWidth - 10, with.PixelHeight - 10)[3], "The window around the panel is in the picture.");
	});

	private static byte[] PixelAt(byte[] png, int x, int y)
	{
		using var image = new Bitmap(new MemoryStream(png));

		var stride = image.PixelSize.Width * 4;
		var length = stride * image.PixelSize.Height;
		var buffer = Marshal.AllocHGlobal(length);

		try
		{
			image.CopyPixels(new PixelRect(image.PixelSize), buffer, length, stride);

			var bytes = new byte[length];

			Marshal.Copy(buffer, bytes, 0, length);

			var at = y * stride + x * 4;

			return [bytes[at], bytes[at + 1], bytes[at + 2], bytes[at + 3]];
		}
		finally
		{
			Marshal.FreeHGlobal(buffer);
		}
	}
}
