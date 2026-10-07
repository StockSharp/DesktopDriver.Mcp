namespace StockSharp.DesktopDriver.Wpf;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// The windows a walk starts from, and the surfaces things are drawn on.
/// </summary>
/// <remarks>
/// Asked each time rather than captured once at startup: windows opened later - a dialog, a floated
/// panel - are as real as the main one, and a tracker that only knew the windows that existed when it
/// started would be blind to exactly the part a test is usually about.
/// </remarks>
public sealed class WpfRootTracker(WpfNodeBinder binder) : IUiRootSource
{
	// What a popup hosts sits within a few levels of the popup's own root; looking deeper would walk the
	// whole of an open menu to find something that is always at the top of it.
	private const int _openerDepth = 8;

	private readonly WpfNodeBinder _binder = binder ?? throw new ArgumentNullException(nameof(binder));

	/// <summary>
	/// Windows this tracker was told about directly, for hosts with no application of their own.
	/// </summary>
	public ImmutableArray<Window> Explicit { get; set; } = ImmutableArray<Window>.Empty;

	/// <inheritdoc />
	public ImmutableArray<UiSubject> GetRoots(UiReadBudget budget)
	{
		var roots = ImmutableArray.CreateBuilder<UiSubject>();

		foreach (var (root, _) in Roots())
		{
			if (roots.Count >= (budget?.MaxNodes ?? int.MaxValue))
				break;

			var reference = _binder.Bind(root);

			if (reference is not null)
				roots.Add(new UiSubject(reference.Id, root));
		}

		return roots.ToImmutable();
	}

	/// <inheritdoc />
	public ImmutableArray<UiSurfaceInfo> GetSurfaces()
	{
		var surfaces = ImmutableArray.CreateBuilder<UiSurfaceInfo>();

		foreach (var (root, owner) in Roots())
		{
			var reference = _binder.Bind(root);
			var client = ClientSize(root);

			surfaces.Add(new UiSurfaceInfo(
				UiAutomationNames.GetSurfaceId(root),
				root is Window ? UiSurfaceKinds.Window : UiSurfaceKinds.Popup,
				Owner(root, owner),
				reference,
				UiField<string>.Known(root is Window titled ? titled.Title ?? string.Empty : string.Empty),
				UiField<UiRect>.Known(new UiRect(0, 0, client.Width, client.Height)),
				UiField<double>.Known(Scaling(root)),
				UiField<bool>.Known(root is not Window active || active.IsActive)));
		}

		return surfaces.ToImmutable();
	}

	private static string Owner(FrameworkElement root, FrameworkElement owner)
	{
		if (root is Window { Owner: Window parent })
			return UiAutomationNames.GetSurfaceId(parent);

		return root is Window ? null : UiAutomationNames.GetSurfaceId(owner);
	}

	// A window measures itself with its frame included, so its own size is the outer one while the single
	// child its template arranges is what fills the client area - and the client area is where the
	// surface's coordinates start, so it is the size everything read off this surface is relative to.
	private static Size ClientSize(FrameworkElement root)
	{
		if (root is Window && VisualTreeHelper.GetChildrenCount(root) > 0 && VisualTreeHelper.GetChild(root, 0) is FrameworkElement content)
			return content.RenderSize;

		return root.RenderSize;
	}

	// Per surface rather than per application: a window dragged to a second monitor is drawn at that
	// monitor's scaling while the rest of the application stays at its own.
	private static double Scaling(Visual root)
	{
		var target = PresentationSource.FromVisual(root)?.CompositionTarget;

		return target is null ? 1 : target.TransformToDevice.M11;
	}

	// The windows, and then every surface opened from one of them: a menu, a combo box's list, a tooltip.
	// A popup is a surface of its own and nothing in the window's tree leads into it, so a walk that only
	// knew about windows would report an open menu as a window with no menu in it.
	private IEnumerable<(FrameworkElement Root, FrameworkElement Owner)> Roots()
	{
		var popups = Popups();
		var queue = new Queue<(FrameworkElement Root, FrameworkElement Owner)>();
		var seen = new HashSet<FrameworkElement>();

		foreach (var window in Windows())
			queue.Enqueue((window, null));

		while (queue.Count > 0)
		{
			var current = queue.Dequeue();

			if (!seen.Add(current.Root))
				continue;

			yield return current;

			foreach (var popup in popups)
			{
				if (!ReferenceEquals(popup.Owner, current.Root) || seen.Contains(popup.Root))
					continue;

				UiAutomationNames.SetSurfaceId(popup.Root, $"popup:{Named(popup.Opener, current.Root)}");
				queue.Enqueue((popup.Root, current.Root));
			}
		}

		// A popup opened from nowhere the walk reaches - placed at absolute coordinates, or opened from a
		// window that has since closed - is still on screen, and leaving it out would report an open menu
		// as absent rather than as a menu nothing owns.
		foreach (var popup in popups)
		{
			if (!seen.Add(popup.Root))
				continue;

			UiAutomationNames.SetSurfaceId(popup.Root, $"popup:{Named(popup.Opener, null)}");

			yield return (popup.Root, null);
		}
	}

	// A popup is a window of its own with a presentation source of its own, so it is not in the visual
	// tree of the window that opened it and the list of sources is the only place it appears. Which
	// surface it belongs to is answered the other way round, through the surface its opener is drawn on.
	private static ImmutableArray<(FrameworkElement Root, FrameworkElement Opener, FrameworkElement Owner)> Popups()
	{
		var popups = ImmutableArray.CreateBuilder<(FrameworkElement Root, FrameworkElement Opener, FrameworkElement Owner)>();

		foreach (var source in PresentationSource.CurrentSources.OfType<PresentationSource>())
		{
			if (source.IsDisposed || source.RootVisual is not FrameworkElement root || root is Window || !root.IsVisible)
				continue;

			var popup = OpenerOf(root, 0);

			if (popup is { IsOpen: false })
				continue;

			var opener = popup?.TemplatedParent as FrameworkElement ?? popup?.PlacementTarget as FrameworkElement ?? popup;

			popups.Add((root, opener, OwnerOf(opener)));
		}

		return popups.ToImmutable();
	}

	// A popup keeps what it hosts as its logical child even though that content is drawn on a window of
	// its own, so the content found under the popup's root leads back to the popup, and the popup to
	// whatever placed it.
	private static Popup OpenerOf(DependencyObject visual, int depth)
	{
		if (depth >= _openerDepth || visual is not Visual)
			return null;

		var count = VisualTreeHelper.GetChildrenCount(visual);

		for (var i = 0; i < count; i++)
		{
			var child = VisualTreeHelper.GetChild(visual, i);

			if (LogicalTreeHelper.GetParent(child) is Popup popup)
				return popup;

			if (OpenerOf(child, depth + 1) is Popup nested)
				return nested;
		}

		return null;
	}

	private static FrameworkElement OwnerOf(FrameworkElement opener)
		=> opener is null ? null : PresentationSource.FromVisual(opener)?.RootVisual as FrameworkElement;

	// Named after what opened it, because every popup root is the same type: two open at once would
	// otherwise be one surface, and a caller asking about the menu would be told about the tooltip.
	private static string Named(FrameworkElement opener, FrameworkElement owner)
	{
		var id = UiAutomationNames.GetNodeId(opener);

		if (id is not null)
			return $"{id.ScopeId}/{id.LocalId}";

		var surface = UiAutomationNames.GetSurfaceId(owner);
		var name = opener?.GetType().Name;

		return surface is null ? name : $"{surface}/{name}";
	}

	private ImmutableArray<Window> Windows()
	{
		if (!Explicit.IsDefaultOrEmpty)
			return Explicit;

		if (Application.Current is { } application)
			return [.. application.Windows.OfType<Window>().Where(window => window.IsVisible)];

		return ImmutableArray<Window>.Empty;
	}
}
