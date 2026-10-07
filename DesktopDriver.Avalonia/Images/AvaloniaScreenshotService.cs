namespace StockSharp.DesktopDriver.Avalonia;

using System;
using System.Collections.Generic;
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

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Draws a control or a window into a picture.
/// </summary>
/// <remarks>
/// What this produces is the control drawing itself, which is not the same evidence as a photograph of
/// the screen: it shows what the control would paint even if another window is over it. The reply says
/// which of the two it is, and a caller that asked for the other one is refused rather than given this.
/// </remarks>
public sealed class AvaloniaScreenshotService(
	IUiNodeRegistry registry,
	IUiAdapterRegistry adapters,
	IUiRevisionTracker revisions,
	IUiExecutor executor,
	UiArtifactStore artifacts,
	Guid instanceId)
{
	// Where each masked thing sits on the window, in the window's own coordinates: the window is what
	// has been drawn, and the cut to the wanted control comes after.
	private IEnumerable<Rect> Masked(TopLevel root, ImmutableArray<UiMask> mask)
	{
		if (mask.IsDefaultOrEmpty)
			yield break;

		foreach (var entry in mask)
		{
			// Looked for when nothing has read it yet: a status bar outside the control being pictured is
			// not in the tree the picture's own reading walked.
			var subject = _registry.Resolve(UiTarget.FromId(entry.Node));

			if (subject.Instance is not Control control)
				throw UiErrors.Unsupported($"{entry.Node} is not a control a picture can leave out.");

			if (entry.Scope == UiMaskScopes.Text)
			{
				if (entry.Part is not null)
					throw UiErrors.Invalid($"The text of {entry.Node} is left out wherever it is drawn, so a part of it means nothing.");

				foreach (var rectangle in PrivateText.Occurrences(root, control))
					yield return rectangle;

				continue;
			}

			var size = control.Bounds.Size;

			if (size.Width <= 0 || size.Height <= 0 || control.TranslatePoint(default, root) is not Point at)
				continue;

			var whole = new Rect(at, size);

			if (entry.Part is null or UiControlPart)
			{
				yield return Painted(control, whole);
				continue;
			}

			var part = Part(subject, entry.Part);

			// Sideways from the part, downwards from where the part ends: a column's values are covered
			// and its heading is left showing, so a reader can see which column was taken out rather than
			// a blank strip where a table should be.
			var below = part.Y + part.Height;

			yield return new(new Point(part.X, below), new Size(part.Width, Math.Max(0, whole.Y + whole.Height - below)));
		}
	}

	// Answered by the same adapter that answers where a click lands, so that a part means here what it
	// means everywhere else.
	private Rect Part(UiSubject subject, UiTargetPart part)
	{
		var reference = _registry.GetReference(subject.Id);
		var context = new UiCaptureContext(reference, _revisions.Read(subject.Id), UiCaptureOptions.Default);

		var located = _adapters.Resolve(subject) is IUiInputTargetAdapter adapter && adapter.SupportsTargetPart(subject, part)
			? adapter.ResolveInputTarget(subject, part, null, context)
			: throw UiErrors.Unsupported($"{subject.Id} cannot say where {part} is.");

		var bounds = located.BoundsInSurfaceDip;

		return new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
	}

	// A caption laid out in a stretched cell is as wide as the cell, so covering the whole of it paints
	// over the empty half of the row as well and the picture reads as a broken one. What is worth
	// covering is where the text was actually drawn, which is what the control asked for.
	private static Rect Painted(Control control, Rect whole)
	{
		var wanted = control.DesiredSize;

		if (wanted.Width <= 0 || wanted.Height <= 0)
			return whole;

		var painted = whole.Intersect(new(whole.Position, wanted));

		return painted.Width <= 0 || painted.Height <= 0 ? whole : painted;
	}

	// The picture as a file. What a mask covers is blurred in the frame in memory before anything is
	// written, so no file and no artifact ever holds what was under it. The areas are in the window's
	// units; offset moves them to where the picture starts.
	private static byte[] Encode(RenderTargetBitmap bitmap, IEnumerable<Rect> covered, Vector offset, double scaling)
	{
		using var stream = new MemoryStream();

		// A glyph's antialiased edge reaches a little past the box its text was laid out in.
		var areas = covered.Select(area => area.Inflate(new Thickness(2, 1)).Translate(offset)).ToArray();

		if (areas.Length == 0)
		{
			bitmap.Save(stream, PngBitmapEncoderOptions.Default);
			return stream.ToArray();
		}

		if (bitmap.Format is not { } format || bitmap.AlphaFormat is not { } alpha)
			throw UiErrors.Unsupported("This platform does not say how its pictures are laid out in memory.");

		var size = bitmap.PixelSize;
		var stride = size.Width * 4;
		var pixels = new byte[stride * size.Height];
		var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);

		try
		{
			bitmap.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), pixels.Length, stride);
		}
		finally
		{
			handle.Free();
		}

		foreach (var area in areas)
			PictureBlur.Apply(pixels, size.Width, size.Height, area, scaling);

		using var blurred = new WriteableBitmap(size, bitmap.Dpi, format, alpha);

		using (var frame = blurred.Lock())
		{
			for (var row = 0; row < size.Height; row++)
				Marshal.Copy(pixels, row * stride, frame.Address + row * frame.RowBytes, stride);
		}

		blurred.Save(stream, PngBitmapEncoderOptions.Default);

		return stream.ToArray();
	}

	private readonly IUiNodeRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
	private readonly IUiAdapterRegistry _adapters = adapters ?? throw new ArgumentNullException(nameof(adapters));
	private readonly IUiRevisionTracker _revisions = revisions ?? throw new ArgumentNullException(nameof(revisions));
	private readonly IUiExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));
	private readonly UiArtifactStore _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
	private readonly Guid _instanceId = instanceId;

	/// <summary>
	/// Takes a picture.
	/// </summary>
	/// <param name="request">What to picture and how.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>Where the picture is kept.</returns>
	public Task<UiScreenshotInfo> CaptureAsync(UiScreenshotRequest request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (request.CaptureKind is not (UiCaptureKinds.ControlRender or UiCaptureKinds.WindowCapture))
		{
			throw UiErrors.Unsupported(
				$"This backend draws the control itself or asks the desktop for a window; it cannot produce a {request.CaptureKind}.");
		}

		return request.CaptureKind == UiCaptureKinds.WindowCapture
			? _executor.InvokeAsync(() => CaptureWindow(request), cancellationToken)
			: _executor.InvokeAsync(() => Capture(request), cancellationToken);
	}

	// The window as the desktop has it, which is the only way its title bar is in the picture: the bar is
	// drawn around the window's contents rather than by them, so a redraw of the contents leaves a strip
	// of nothing where the icon, the title and the buttons are.
	private UiScreenshotInfo CaptureWindow(UiScreenshotRequest request)
	{
		var subject = _registry.Resolve(request.Target);

		if (subject.Instance is not Window window)
			throw UiErrors.Unsupported("Only a window can be photographed the way the desktop has it.");

		var scaling = window.RenderScaling;
		var dpi = new Vector(96 * scaling, 96 * scaling);

		var taken = WindowSurfaceCapture.Take(window, dpi);

		using var picture = taken.Image;

		var width = picture.PixelSize.Width;
		var height = picture.PixelSize.Height;

		if (request.MaxPixelWidth > 0 && width > request.MaxPixelWidth)
			throw UiErrors.LimitExceeded($"That window is {width} pixels wide.");

		if (request.MaxPixelHeight > 0 && height > request.MaxPixelHeight)
			throw UiErrors.LimitExceeded($"That window is {height} pixels tall.");

		var frame = new Size(width / scaling, height / scaling);

		using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), dpi);

		using (var context = bitmap.CreateDrawingContext())
			context.DrawImage(picture, new Rect(default, frame));

		// What a mask covers is placed against the window's contents, and the picture starts at the
		// outside of its frame, so everything covered moves by the difference between the two.
		var data = Encode(bitmap, Masked(window, request.Mask), ContentOffset(window, taken.Origin), scaling);
		var id = _artifacts.Add(data, "image/png");

		return new UiScreenshotInfo(
			id,
			"image/png",
			width,
			height,
			data.Length,
			new UiSnapshotStamp(
				Serialization.UiJson.SchemaVersion,
				_instanceId,
				Guid.NewGuid(),
				DateTime.UtcNow,
				_revisions.Read(subject.Id),
				UiConsistencies.UiThreadRead),
			UiCaptureKinds.WindowCapture,
			UiField<double>.Known(scaling),
			[]);
	}

	// Where the window's contents begin inside the picture of it. Asked of the desktop rather than worked
	// out from a border width, because a window that has taken its frame over draws it where it likes.
	private static Vector ContentOffset(Window window, PixelPoint origin)
	{
		var content = window.PointToScreen(default);

		// The difference is in screen pixels; the drawing is in the window's own units, which is what
		// dividing by the scaling gives back.
		return new(
			(content.X - origin.X) / window.RenderScaling,
			(content.Y - origin.Y) / window.RenderScaling);
	}

	private UiScreenshotInfo Capture(UiScreenshotRequest request)
	{
		var subject = _registry.Resolve(request.Target);

		if (subject.Instance is not Control control)
			throw UiErrors.Unsupported("That node is not something that can draw itself.");

		var size = control.Bounds.Size;

		if (size.Width <= 0 || size.Height <= 0)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, "That control has no size to draw.");

		var root = TopLevel.GetTopLevel(control)
			?? throw UiErrors.Fail(UiErrorCodes.NotCreated, "That control is not in a window.");

		if (control.TranslatePoint(default, root) is not Point origin)
			throw UiErrors.Fail(UiErrorCodes.NotCreated, "That control has not been laid out.");

		var scaling = root.RenderScaling;
		var width = (int)Math.Ceiling(size.Width * scaling);
		var height = (int)Math.Ceiling(size.Height * scaling);

		if (request.MaxPixelWidth > 0 && width > request.MaxPixelWidth)
			throw UiErrors.LimitExceeded($"That control is {width} pixels wide.");

		if (request.MaxPixelHeight > 0 && height > request.MaxPixelHeight)
			throw UiErrors.LimitExceeded($"That control is {height} pixels tall.");

		var dpi = new Vector(96 * scaling, 96 * scaling);

		// A control paints only what it owns. One that takes its ground from the window - a workspace, a
		// page, most panels - drawn on its own comes back as text on transparency. The window is drawn
		// instead and the control's rectangle cut out of it, which is what is behind the control as well
		// as what is in it.
		var frame = root.Bounds.Size;

		using var window = new RenderTargetBitmap(
			new PixelSize((int)Math.Ceiling(frame.Width * scaling), (int)Math.Ceiling(frame.Height * scaling)),
			dpi);

		window.Render(root);

		using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), dpi);

		using (var context = bitmap.CreateDrawingContext())
		using (context.PushClip(new Rect(0, 0, size.Width, size.Height)))
		using (context.PushTransform(Matrix.CreateTranslation(-origin.X, -origin.Y)))
			context.DrawImage(window, new Rect(default, frame));

		var data = Encode(bitmap, Masked(root, request.Mask), new(-origin.X, -origin.Y), scaling);
		var id = _artifacts.Add(data, "image/png");

		return new UiScreenshotInfo(
			id,
			"image/png",
			width,
			height,
			data.Length,
			new UiSnapshotStamp(
				Serialization.UiJson.SchemaVersion,
				_instanceId,
				Guid.NewGuid(),
				DateTime.UtcNow,
				_revisions.Read(subject.Id),
				UiConsistencies.UiThreadRead),
			UiCaptureKinds.ControlRender,
			UiField<double>.Known(scaling),
			[]);
	}
}
