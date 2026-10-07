namespace StockSharp.DesktopDriver.Wpf;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Serialization;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Draws a control into a picture, or photographs a window the way the desktop has it.
/// </summary>
/// <remarks>
/// A control drawing itself is not the same evidence as a photograph of the screen: it shows what the control
/// would paint even if another window is over it, and a window drawn that way has no frame the system drew.
/// The reply says which of the two a picture is.
/// </remarks>
public sealed class WpfScreenshotService(
	IUiNodeRegistry registry,
	IUiAdapterRegistry adapters,
	IUiRevisionTracker revisions,
	IUiExecutor executor,
	UiArtifactStore artifacts,
	Guid instanceId)
{
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

	// The window as the desktop has it, which is the only way a frame the system draws is in the picture.
	private UiScreenshotInfo CaptureWindow(UiScreenshotRequest request)
	{
		var subject = _registry.Resolve(request.Target);

		if (subject.Instance is not Window window)
			throw UiErrors.Unsupported("Only a window can be photographed the way the desktop has it.");

		var scaling = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformToDevice.M11 ?? 1d;
		var dpi = 96 * scaling;

		var taken = WindowSurfaceCapture.Take(window, dpi);
		var width = taken.Image.PixelWidth;
		var height = taken.Image.PixelHeight;

		if (request.MaxPixelWidth > 0 && width > request.MaxPixelWidth)
			throw UiErrors.LimitExceeded($"That window is {width} pixels wide.");

		if (request.MaxPixelHeight > 0 && height > request.MaxPixelHeight)
			throw UiErrors.LimitExceeded($"That window is {height} pixels tall.");

		// What a mask covers is placed against the window's contents, and the picture starts at the outside of its
		// frame, so everything covered moves by the difference between the two - asked of the desktop rather than
		// worked out from a border width, because a window that has taken its frame over draws it where it likes.
		var content = window.PointToScreen(default);
		var offset = new Vector((content.X - taken.Origin.X) / scaling, (content.Y - taken.Origin.Y) / scaling);

		var drawing = new DrawingVisual();

		using (var context = drawing.RenderOpen())
		{
			context.DrawImage(taken.Image, new Rect(0, 0, width / scaling, height / scaling));
			context.PushTransform(new TranslateTransform(offset.X, offset.Y));

			foreach (var rectangle in Masked(window, request.Mask))
				Redact(context, rectangle);

			context.Pop();
		}

		var bitmap = new RenderTargetBitmap(width, height, dpi, dpi, PixelFormats.Pbgra32);

		bitmap.Render(drawing);

		return Stored(bitmap, subject, UiCaptureKinds.WindowCapture, scaling);
	}

	// Where each masked thing sits on the window, in the window's own coordinates: the window is what
	// has been drawn, and the cut to the wanted control comes after.
	private IEnumerable<Rect> Masked(Visual root, ImmutableArray<UiMask> mask)
	{
		if (mask.IsDefaultOrEmpty)
			yield break;

		foreach (var entry in mask)
		{
			// Refused rather than skipped: a picture taken without it would carry the very text it names.
			if (entry.Scope != UiMaskScopes.Node)
				throw UiErrors.Unsupported($"This backend cannot find where the text of {entry.Node} is drawn.");

			if (!_registry.TryResolve(entry.Node, out var subject) || subject.Instance is not FrameworkElement element)
				throw UiErrors.Fail(UiErrorCodes.NotFound, $"Nothing to paint over is registered as {entry.Node}.");

			var size = new Size(element.ActualWidth, element.ActualHeight);

			if (size.Width <= 0 || size.Height <= 0)
				continue;

			var whole = new Rect(element.TransformToAncestor(root).Transform(default), size);

			if (entry.Part is null or UiControlPart)
			{
				yield return Painted(element, whole);
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

	// A caption laid out in a stretched cell is as wide as the cell, so covering the whole of it paints
	// over the empty half of the row as well and the picture reads as a broken one. What is worth
	// covering is where the text was actually drawn, which is what the element asked for.
	private static Rect Painted(FrameworkElement element, Rect whole)
	{
		var wanted = element.DesiredSize;

		if (wanted.Width <= 0 || wanted.Height <= 0)
			return whole;

		var painted = Rect.Intersect(whole, new(whole.Location, wanted));

		return painted.IsEmpty ? whole : painted;
	}

	// Opaque, not blurred or pixellated. What is covered here is a short string - an address, a machine
	// id - and a blur of one is read back by trying the few shapes it could have been. Grey and rounded
	// rather than a black hole, so the picture shows something deliberately taken out.
	private static readonly Brush _redaction = Frozen(Color.FromRgb(0x5A, 0x5A, 0x5A));
	private static readonly Pen _redactionEdge = Frozen(new Pen(Frozen(Color.FromRgb(0x82, 0x82, 0x82)), 1));

	private static Brush Frozen(Color color)
	{
		var brush = new SolidColorBrush(color);

		brush.Freeze();

		return brush;
	}

	private static Pen Frozen(Pen pen)
	{
		pen.Freeze();

		return pen;
	}

	private static void Redact(DrawingContext context, Rect rectangle)
	{
		rectangle.Inflate(2, 1);

		context.DrawRoundedRectangle(_redaction, _redactionEdge, rectangle, 3, 3);
	}

	// Answered by the same adapter that answers where a click lands, so that a part means here what it
	// means everywhere else.
	private UiRect Part(UiSubject subject, UiTargetPart part)
	{
		var reference = _registry.GetReference(subject.Id);
		var context = new UiCaptureContext(reference, _revisions.Read(subject.Id), UiCaptureOptions.Default);

		return _adapters.Resolve(subject) is IUiInputTargetAdapter adapter && adapter.SupportsTargetPart(subject, part)
			? adapter.ResolveInputTarget(subject, part, null, context).BoundsInSurfaceDip
			: throw UiErrors.Unsupported($"{subject.Id} cannot say where {part} is.");
	}

	private UiScreenshotInfo Capture(UiScreenshotRequest request)
	{
		var subject = _registry.Resolve(request.Target);

		if (subject.Instance is not FrameworkElement control)
			throw UiErrors.Unsupported("That node is not something that can draw itself.");

		var size = control.RenderSize;

		if (size.Width <= 0 || size.Height <= 0)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, "That control has no size to draw.");

		// The window the control actually lives in - a popup has one of its own - and the scaling of the
		// display that window is on.
		var source = PresentationSource.FromVisual(control)
			?? throw UiErrors.Fail(UiErrorCodes.NotCreated, "That control is not in a window.");

		if (source.RootVisual is not FrameworkElement root)
			throw UiErrors.Fail(UiErrorCodes.NotCreated, "That control is not in a window.");

		if (!control.TransformToAncestor(root).TryTransform(default, out var origin))
			throw UiErrors.Fail(UiErrorCodes.NotCreated, "That control has not been laid out.");

		var scaling = source.CompositionTarget?.TransformToDevice.M11 ?? 1d;
		var width = (int)Math.Ceiling(size.Width * scaling);
		var height = (int)Math.Ceiling(size.Height * scaling);

		if (request.MaxPixelWidth > 0 && width > request.MaxPixelWidth)
			throw UiErrors.LimitExceeded($"That control is {width} pixels wide.");

		if (request.MaxPixelHeight > 0 && height > request.MaxPixelHeight)
			throw UiErrors.LimitExceeded($"That control is {height} pixels tall.");

		// A control paints only what it owns. One that takes its ground from the window - a workspace, a
		// page, most panels - drawn on its own comes back as text on transparency. The window is drawn
		// instead and the control's rectangle cut out of it, which is what is behind the control as well
		// as what is in it.
		var frame = root.RenderSize;

		// The window's own coordinates are kept: its rectangle is mapped onto a rectangle of the same
		// size, so the origin read above still points at the control once the brush is painted.
		var window = new VisualBrush(root)
		{
			ViewboxUnits = BrushMappingMode.Absolute,
			Viewbox = new Rect(default, frame),
			Stretch = Stretch.Fill,
		};

		var drawing = new DrawingVisual();

		using (var context = drawing.RenderOpen())
		{
			context.PushClip(new RectangleGeometry(new Rect(default, size)));
			context.PushTransform(new TranslateTransform(-origin.X, -origin.Y));
			context.DrawRectangle(window, null, new Rect(default, frame));

			// Painted in the same pass that draws the window, so that what it covers is never in a
			// picture at all - a caption naming whoever is signed in is left out of one rather than
			// drawn and then removed.
			foreach (var rectangle in Masked(root, request.Mask))
				Redact(context, rectangle);

			context.Pop();
			context.Pop();
		}

		var dpi = 96 * scaling;
		var bitmap = new RenderTargetBitmap(width, height, dpi, dpi, PixelFormats.Pbgra32);

		bitmap.Render(drawing);

		return Stored(bitmap, subject, UiCaptureKinds.ControlRender, scaling);
	}

	private UiScreenshotInfo Stored(BitmapSource bitmap, UiSubject subject, string kind, double scaling)
	{
		var encoder = new PngBitmapEncoder();

		encoder.Frames.Add(BitmapFrame.Create(bitmap));

		using var stream = new MemoryStream();

		encoder.Save(stream);

		var data = stream.ToArray();
		var id = _artifacts.Add(data, "image/png");

		return new UiScreenshotInfo(
			id,
			"image/png",
			bitmap.PixelWidth,
			bitmap.PixelHeight,
			data.Length,
			new UiSnapshotStamp(
				UiJson.SchemaVersion,
				_instanceId,
				Guid.NewGuid(),
				DateTime.UtcNow,
				_revisions.Read(subject.Id),
				UiConsistencies.UiThreadRead),
			kind,
			UiField<double>.Known(scaling),
			[]);
	}
}
