namespace StockSharp.DesktopDriver.Maui;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.Maui.Controls;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// The windows of a MAUI application, which are where every walk of its interface starts.
/// </summary>
/// <param name="binder">Registers the windows it finds.</param>
public sealed class MauiRootTracker(MauiNodeBinder binder) : IUiRootSource
{
	private readonly MauiNodeBinder _binder = binder ?? throw new ArgumentNullException(nameof(binder));

	/// <summary>
	/// The windows to read instead of the application's own, for a reader that was handed them.
	/// </summary>
	public ImmutableArray<Window> Explicit { get; set; } = ImmutableArray<Window>.Empty;

	/// <inheritdoc />
	public ImmutableArray<UiSubject> GetRoots(UiReadBudget budget)
	{
		var roots = ImmutableArray.CreateBuilder<UiSubject>();

		foreach (var window in Windows())
		{
			if (roots.Count >= (budget?.MaxNodes ?? int.MaxValue))
				break;

			var reference = _binder.Bind(window);

			if (reference is not null)
				roots.Add(new UiSubject(reference.Id, window));
		}

		return roots.ToImmutable();
	}

	/// <inheritdoc />
	public ImmutableArray<UiSurfaceInfo> GetSurfaces()
	{
		var surfaces = ImmutableArray.CreateBuilder<UiSurfaceInfo>();

		foreach (var window in Windows())
		{
			var reference = _binder.Bind(window);
			var client = MauiPlatform.ClientSize(window);
			var scaling = MauiPlatform.Scaling(window);

			surfaces.Add(new UiSurfaceInfo(
				UiAutomationNames.GetSurfaceId(window),
				UiSurfaceKinds.Window,
				null,
				reference,
				UiField<string>.Known(window.Title ?? string.Empty),
				client is { } size
					? UiField<UiRect>.Known(new UiRect(0, 0, size.Width, size.Height))
					: UiField<UiRect>.Unavailable(UiUnavailableReasons.NotLoaded, "not shown"),
				scaling is { } known
					? UiField<double>.Known(known)
					: UiField<double>.Unavailable(UiUnavailableReasons.NotLoaded, "not shown"),
				Active(window)));
		}

		return surfaces.ToImmutable();
	}

	private static UiField<bool> Active(Window window)
		=> MauiPlatform.IsForeground(window) is { } active
			? UiField<bool>.Known(active)
			: UiField<bool>.Unavailable(UiUnavailableReasons.NotLoaded, "not shown");

	private IEnumerable<Window> Windows()
	{
		if (!Explicit.IsDefaultOrEmpty)
			return Explicit;

		return Application.Current?.Windows.OfType<Window>() ?? [];
	}
}
