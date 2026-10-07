namespace StockSharp.DesktopDriver.Maui;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Maui.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Serialization;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

using MauiRect = Microsoft.Maui.Graphics.Rect;
using WinElementTheme = Microsoft.UI.Xaml.ElementTheme;

/// <summary>
/// Draws an element of a MAUI application into a picture.
/// </summary>
/// <param name="registry">Where the named node is looked up.</param>
/// <param name="adapters">Where a part of a control is measured.</param>
/// <param name="revisions">The revisions a picture is stamped with.</param>
/// <param name="executor">Runs the drawing on the interface's thread.</param>
/// <param name="artifacts">Where the picture is kept for the caller to fetch.</param>
/// <param name="instanceId">The running application the picture is of.</param>
public sealed class MauiScreenshotService(
	IUiNodeRegistry registry,
	IUiAdapterRegistry adapters,
	IUiRevisionTracker revisions,
	MauiUiExecutor executor,
	UiArtifactStore artifacts,
	Guid instanceId)
{
	// Opaque, not blurred or pixellated. What is covered is a short string - an address, a machine id - and a
	// blur of one is read back by trying the few shapes it could have been. Grey with a lighter edge rather
	// than a black hole, so the picture shows something deliberately taken out.
	private const uint _redaction = 0xFF5A5A5A;
	private const uint _redactionEdge = 0xFF828282;

	// What the system paints behind a window that sets no backdrop of its own, in each theme.
	private const uint _darkBackdrop = 0xFF000000;
	private const uint _lightBackdrop = 0xFFFFFFFF;

	private readonly IUiNodeRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
	private readonly IUiAdapterRegistry _adapters = adapters ?? throw new ArgumentNullException(nameof(adapters));
	private readonly IUiRevisionTracker _revisions = revisions ?? throw new ArgumentNullException(nameof(revisions));
	private readonly MauiUiExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));
	private readonly UiArtifactStore _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
	private readonly Guid _instanceId = instanceId;

	/// <summary>
	/// Takes a picture.
	/// </summary>
	/// <param name="request">What to draw.</param>
	/// <param name="cancellationToken">Stops the drawing before it starts.</param>
	/// <returns>The picture, kept as an artifact.</returns>
	public Task<UiScreenshotInfo> CaptureAsync(UiScreenshotRequest request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (request.CaptureKind != UiCaptureKinds.ControlRender)
		{
			throw UiErrors.Unsupported(
				$"This backend draws the element itself; it cannot produce a {request.CaptureKind}.");
		}

		return _executor.InvokeAsync(() => CaptureAsync(request), cancellationToken);
	}

	private async Task<UiScreenshotInfo> CaptureAsync(UiScreenshotRequest request)
	{
		var subject = _registry.Resolve(request.Target);

		if (subject.Instance is not Element element)
			throw UiErrors.Unsupported("That node is not something that can draw itself.");

		var window = UiAutomationNames.GetWindow(element)
			?? throw UiErrors.Fail(UiErrorCodes.NotCreated, "That element is not in a window.");

		var root = MauiPlatform.RootOf(window)
			?? throw UiErrors.Fail(UiErrorCodes.NotCreated, "That window is not shown.");

		var area = element is Window
			? new MauiRect(0, 0, root.ActualWidth, root.ActualHeight)
			: MauiPlatform.BoundsInWindow(MauiPlatform.ViewOf(element)
				?? throw UiErrors.Fail(UiErrorCodes.NotCreated, "That element is not drawn by itself."))
				?? throw UiErrors.Fail(UiErrorCodes.NotCreated, "That element has not been laid out.");

		if (area.Width <= 0 || area.Height <= 0)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, "That element has no size to draw.");

		var scaling = MauiPlatform.Scaling(window) ?? 1d;
		var width = (int)Math.Ceiling(area.Width * scaling);
		var height = (int)Math.Ceiling(area.Height * scaling);

		if (request.MaxPixelWidth > 0 && width > request.MaxPixelWidth)
			throw UiErrors.LimitExceeded($"That element is {width} pixels wide.");

		if (request.MaxPixelHeight > 0 && height > request.MaxPixelHeight)
			throw UiErrors.LimitExceeded($"That element is {height} pixels tall.");

		// Read before the window is drawn, so that what is covered is worked out against the same layout the
		// picture shows.
		var masked = Masked(request.Mask);

		// An element paints only what it owns. One that takes its ground from the page - most of them - drawn on
		// its own comes back as text on transparency, so the window is drawn instead and the element's
		// rectangle cut out of it, which is what is behind the element as well as what is in it.
		var bitmap = new RenderTargetBitmap();

		await bitmap.RenderAsync(root);

		var pixels = (await bitmap.GetPixelsAsync()).ToArray();
		var frameWidth = bitmap.PixelWidth;
		var frameHeight = bitmap.PixelHeight;

		// The window is drawn at whatever scaling its display has, which is read off the drawing rather than
		// assumed, so that a rectangle in device independent pixels lands on the same pixels the picture has.
		var scale = root.ActualWidth > 0 ? frameWidth / root.ActualWidth : scaling;

		Backdrop(pixels, root.ActualTheme == WinElementTheme.Dark ? _darkBackdrop : _lightBackdrop);

		foreach (var rectangle in masked)
			Redact(pixels, frameWidth, frameHeight, rectangle, scale);

		var cut = Cut(pixels, frameWidth, frameHeight, area, scale, out var cutWidth, out var cutHeight);
		var data = await EncodeAsync(cut, cutWidth, cutHeight, scale);
		var id = _artifacts.Add(data, "image/png");

		return new UiScreenshotInfo(
			id,
			"image/png",
			cutWidth,
			cutHeight,
			data.Length,
			new UiSnapshotStamp(
				UiJson.SchemaVersion,
				_instanceId,
				Guid.NewGuid(),
				DateTime.UtcNow,
				_revisions.Read(subject.Id),
				UiConsistencies.UiThreadRead),
			UiCaptureKinds.ControlRender,
			UiField<double>.Known(scale),
			[]);
	}

	// Where each masked thing sits on the window, in the window's own coordinates: the window is what has been
	// drawn, and the cut to the wanted element comes after.
	private List<MauiRect> Masked(ImmutableArray<UiMask> mask)
	{
		var rectangles = new List<MauiRect>();

		if (mask.IsDefaultOrEmpty)
			return rectangles;

		foreach (var entry in mask)
		{
			// Refused rather than skipped: a picture taken without it would carry the very text it names.
			if (entry.Scope != UiMaskScopes.Node)
				throw UiErrors.Unsupported($"This backend cannot find where the text of {entry.Node} is drawn.");

			if (!_registry.TryResolve(entry.Node, out var subject) || subject.Instance is not Element element)
				throw UiErrors.Fail(UiErrorCodes.NotFound, $"Nothing to paint over is registered as {entry.Node}.");

			if (MauiPlatform.BoundsInWindow(MauiPlatform.ViewOf(element)) is not { Width: > 0, Height: > 0 } whole)
				continue;

			if (entry.Part is null or UiControlPart)
			{
				rectangles.Add(whole);
				continue;
			}

			var part = Part(subject, entry.Part);

			// Sideways from the part, downwards from where the part ends: a column's values are covered and its
			// heading is left showing, so a reader can see which column was taken out.
			var below = part.Y + part.Height;

			rectangles.Add(new MauiRect(part.X, below, part.Width, Math.Max(0, whole.Y + whole.Height - below)));
		}

		return rectangles;
	}

	// Answered by the same adapter that answers where a click lands, so that a part means here what it means
	// everywhere else.
	private UiRect Part(UiSubject subject, UiTargetPart part)
	{
		var reference = _registry.GetReference(subject.Id);
		var context = new UiCaptureContext(reference, _revisions.Read(subject.Id), UiCaptureOptions.Default);

		return _adapters.Resolve(subject) is IUiInputTargetAdapter adapter && adapter.SupportsTargetPart(subject, part)
			? adapter.ResolveInputTarget(subject, part, null, context).BoundsInSurfaceDip
			: throw UiErrors.Unsupported($"{subject.Id} cannot say where {part} is.");
	}

	// The strip a window keeps for its title, and anything else it leaves see-through, is painted by the system
	// behind the content, and a drawing of the content has nothing there. Laid over what the system paints, so
	// that the picture shows the window rather than a hole at its top.
	private static void Backdrop(byte[] pixels, uint backdrop)
	{
		var blue = (byte)backdrop;
		var green = (byte)(backdrop >> 8);
		var red = (byte)(backdrop >> 16);

		for (var index = 0; index < pixels.Length; index += 4)
		{
			var alpha = pixels[index + 3];

			if (alpha == 255)
				continue;

			// The drawing is premultiplied, so what shows through is the backdrop scaled by what is left of it.
			var through = 255 - alpha;

			pixels[index] = (byte)(pixels[index] + blue * through / 255);
			pixels[index + 1] = (byte)(pixels[index + 1] + green * through / 255);
			pixels[index + 2] = (byte)(pixels[index + 2] + red * through / 255);
			pixels[index + 3] = 255;
		}
	}

	private static void Redact(byte[] pixels, int width, int height, MauiRect rectangle, double scale)
	{
		var left = Math.Max(0, (int)Math.Floor((rectangle.X - 2) * scale));
		var top = Math.Max(0, (int)Math.Floor((rectangle.Y - 1) * scale));
		var right = Math.Min(width, (int)Math.Ceiling((rectangle.Right + 2) * scale));
		var bottom = Math.Min(height, (int)Math.Ceiling((rectangle.Bottom + 1) * scale));

		for (var y = top; y < bottom; y++)
		{
			for (var x = left; x < right; x++)
			{
				var edge = y == top || y == bottom - 1 || x == left || x == right - 1;

				BitConverter.TryWriteBytes(pixels.AsSpan((y * width + x) * 4, 4), edge ? _redactionEdge : _redaction);
			}
		}
	}

	private static byte[] Cut(byte[] pixels, int width, int height, MauiRect area, double scale, out int cutWidth, out int cutHeight)
	{
		var left = Math.Clamp((int)Math.Round(area.X * scale), 0, width);
		var top = Math.Clamp((int)Math.Round(area.Y * scale), 0, height);

		cutWidth = Math.Clamp((int)Math.Round(area.Width * scale), 1, width - left);
		cutHeight = Math.Clamp((int)Math.Round(area.Height * scale), 1, height - top);

		var cut = new byte[cutWidth * cutHeight * 4];

		for (var row = 0; row < cutHeight; row++)
			Array.Copy(pixels, ((top + row) * width + left) * 4, cut, row * cutWidth * 4, cutWidth * 4);

		return cut;
	}

	private static async Task<byte[]> EncodeAsync(byte[] pixels, int width, int height, double scale)
	{
		using var stream = new InMemoryRandomAccessStream();

		var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
		var dpi = 96 * scale;

		encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)width, (uint)height, dpi, dpi, pixels);

		await encoder.FlushAsync();

		stream.Seek(0);

		using var output = new MemoryStream();

		await stream.AsStreamForRead().CopyToAsync(output);

		return output.ToArray();
	}
}
