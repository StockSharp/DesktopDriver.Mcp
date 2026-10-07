namespace StockSharp.DesktopDriver.Avalonia;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.VisualTree;

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
public sealed class AvaloniaRootTracker(AvaloniaNodeBinder binder) : IUiRootSource
{
	private readonly AvaloniaNodeBinder _binder = binder ?? throw new ArgumentNullException(nameof(binder));

	/// <summary>
	/// Windows this tracker was told about directly, for hosts with no classic desktop lifetime.
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

			surfaces.Add(new UiSurfaceInfo(
				UiAutomationNames.GetSurfaceId(root),
				root is Window ? UiSurfaceKinds.Window : UiSurfaceKinds.Popup,
				Owner(root, owner),
				reference,
				UiField<string>.Known(root is Window window ? window.Title ?? string.Empty : string.Empty),
				UiField<UiRect>.Known(new UiRect(0, 0, root.ClientSize.Width, root.ClientSize.Height)),
				UiField<double>.Known(root.RenderScaling),
				UiField<bool>.Known(root is not WindowBase visible || visible.IsActive)));
		}

		return surfaces.ToImmutable();
	}

	private static string Owner(TopLevel root, TopLevel owner)
	{
		if (root is Window { Owner: Window parent })
			return UiAutomationNames.GetSurfaceId(parent);

		return root is Window ? null : UiAutomationNames.GetSurfaceId(owner);
	}

	// The windows, and then every surface opened from one of them: a menu, a combo box's list, a tooltip.
	// A popup is a top level of its own and nothing in the window's tree leads into it, so a walk that
	// only knew about windows would report an open menu as a window with no menu in it.
	private IEnumerable<(TopLevel Root, TopLevel Owner)> Roots()
	{
		var queue = new Queue<(TopLevel Root, TopLevel Owner)>();
		var seen = new HashSet<TopLevel>();

		foreach (var window in Windows())
			queue.Enqueue((window, null));

		while (queue.Count > 0)
		{
			var current = queue.Dequeue();

			if (!seen.Add(current.Root))
				continue;

			yield return current;

			foreach (var (popup, opener) in PopupsOf(current.Root))
			{
				UiAutomationNames.SetSurfaceId(popup, $"popup:{Named(opener, current.Root)}");
				queue.Enqueue((popup, current.Root));
			}
		}
	}

	private static IEnumerable<(TopLevel Popup, Control Opener)> PopupsOf(TopLevel root)
	{
		foreach (var control in root.GetVisualDescendants().OfType<Control>())
		{
			// Found through the content rather than through the popup's host, which the library keeps to
			// itself. A popup drawn inside its own window comes back as a surface of its own; one drawn in
			// the window's overlay comes back as the window, and is already being walked.
			if (control is Popup { IsOpen: true, Child: { } child } popup &&
				TopLevel.GetTopLevel(child) is { } hosted && !ReferenceEquals(hosted, root))
			{
				yield return (hosted, popup.TemplatedParent as Control ?? popup.PlacementTarget ?? popup);
			}

			// A context menu keeps its popup to itself rather than in the window, so it is found through the
			// control it was opened on.
			if (control.ContextMenu is { IsOpen: true } menu &&
				TopLevel.GetTopLevel(menu) is { } menuHost && !ReferenceEquals(menuHost, root))
			{
				yield return (menuHost, control);
			}

			// So does a flyout, which is found through what it shows. What opens one is often a part of a composed
			// control with no name of its own, so the flyout is named after the nearest control that has one.
			if (FlyoutOf(control) is { IsOpen: true } flyout &&
				ShownBy(flyout) is { } shown &&
				TopLevel.GetTopLevel(shown) is { } flyoutHost && !ReferenceEquals(flyoutHost, root))
			{
				yield return (flyoutHost, NamedSelfOrAncestor(control));
			}
		}
	}

	private static FlyoutBase FlyoutOf(Control control)
		=> control switch
		{
			Button { Flyout: { } flyout } => flyout,
			SplitButton { Flyout: { } flyout } => flyout,
			_ => FlyoutBase.GetAttachedFlyout(control),
		};

	private static Control ShownBy(FlyoutBase flyout)
		=> flyout switch
		{
			Flyout { Content: Control content } => content,
			MenuFlyout menu => menu.Items.OfType<Control>().FirstOrDefault(),
			_ => null,
		};

	private static Control NamedSelfOrAncestor(Control control)
		=> control.GetSelfAndVisualAncestors()
			.OfType<Control>()
			.FirstOrDefault(candidate => !string.IsNullOrEmpty(AutomationProperties.GetAutomationId(candidate))) ?? control;

	// Named after what opened it, because every popup root is the same type: two open at once would
	// otherwise be one surface, and a caller asking about the menu would be told about the tooltip.
	private static string Named(Control opener, TopLevel owner)
	{
		var id = UiAutomationNames.GetNodeId(opener);

		return id is null
			? $"{UiAutomationNames.GetSurfaceId(owner)}/{opener?.GetType().Name}"
			: $"{id.ScopeId}/{id.LocalId}";
	}

	private ImmutableArray<Window> Windows()
	{
		if (!Explicit.IsDefaultOrEmpty)
			return Explicit;

		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			return [.. desktop.Windows.Where(window => window.IsVisible)];

		return ImmutableArray<Window>.Empty;
	}
}
