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
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Avalonia;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
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

		var plain = await Pixels(service, artifacts, pageId, []);
		var masked = await Pixels(service, artifacts, pageId, [new UiMask(secretId, null, UiMaskScopes.Node)]);

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

		var plain = await Pixels(service, artifacts, pageId, []);
		var masked = await Pixels(service, artifacts, pageId, [new UiMask(userId, null, UiMaskScopes.Text)]);

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
				[new UiMask(userId, null, UiMaskScopes.Text)]),
			CancellationToken.None));

		AreEqual(UiErrorCodes.NotCreated, error.Error.Code);
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
				[new UiMask(new UiNodeId("window:MissingMaskWindow", "NoSuchThing"), null, UiMaskScopes.Node)]),
			CancellationToken.None));
	});

	// Decoded rather than compared as bytes: a PNG of a flat rectangle compresses differently, so the
	// length of the file says nothing about what is in the picture.
	private static async Task<Drawn> Pixels(
		AvaloniaScreenshotService service,
		UiArtifactStore artifacts,
		UiNodeId target,
		ImmutableArray<UiMask> mask)
	{
		var info = await service.CaptureAsync(
			new UiScreenshotRequest(UiTarget.FromId(target), UiCaptureKinds.ControlRender, false, 0, 0, null, mask),
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
		{
			var start = line.Text.IndexOf(text, StringComparison.Ordinal);

			if (start < 0)
				throw new ArgumentException($"'{line.Text}' does not hold '{text}'.", nameof(text));

			var origin = line.TranslatePoint(default, page) ?? throw new InvalidOperationException("The line is not on the page.");
			var drawn = line.TextLayout.HitTestTextRange(start, text.Length).Aggregate((left, right) => left.Union(right));
			var scaling = TopLevel.GetTopLevel(page)?.RenderScaling ?? 1;

			return new(
				(int)Math.Floor((origin.X + drawn.X) * scaling),
				(int)Math.Floor((origin.Y + drawn.Y) * scaling),
				(int)Math.Ceiling(drawn.Width * scaling),
				(int)Math.Ceiling(drawn.Height * scaling));
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
			new UiScreenshotRequest(UiTarget.FromId(id), UiCaptureKinds.ControlRender, false, 0, 0, null, []),
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
			new UiScreenshotRequest(UiTarget.FromId(id), UiCaptureKinds.ScreenCapture, false, 0, 0, null, []),
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
			new UiScreenshotRequest(UiTarget.FromId(id), UiCaptureKinds.ControlRender, false, 0, 0, null, []),
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
			new UiScreenshotRequest(UiTarget.FromId(id), UiCaptureKinds.ControlRender, false, 0, 0, null, []),
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
