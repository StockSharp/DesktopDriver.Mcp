namespace StockSharp.DesktopDriver.Maui;

using System;
using System.Linq;
using System.Reflection;

using Microsoft.Maui.Controls;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;

using WinElement = Microsoft.UI.Xaml.FrameworkElement;
using WinNavigationItem = Microsoft.UI.Xaml.Controls.NavigationViewItem;

/// <summary>
/// Reads an entry of a shell and says where it is drawn, so that it can be clicked like the line of the
/// flyout it is.
/// </summary>
/// <param name="binder">Registers the elements it reports.</param>
public sealed class MauiShellItemAdapter(MauiNodeBinder binder)
	: MauiControlAdapter(binder), IUiInputTargetAdapter
{
	/// <inheritdoc />
	public override string Kind => "tabItem";

	/// <inheritdoc />
	public override bool CanHandle(UiSubject subject) => subject?.Instance is BaseShellItem;

	/// <inheritdoc />
	public bool SupportsTargetPart(UiSubject subject, UiTargetPart part)
		=> CanHandle(subject) && part is UiControlPart;

	/// <inheritdoc />
	public UiResolvedInputTarget ResolveInputTarget(
		UiSubject subject,
		UiTargetPart part,
		UiInputAction action,
		UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var item = (BaseShellItem)subject.Instance;

		var window = UiAutomationNames.GetWindow(item)
			?? throw UiErrors.Fail(UiErrorCodes.NotCreated, "That entry is not in a window.");

		if (!MauiReach.IsVisible(item))
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, "That entry is not visible.");

		if (!MauiReach.IsEnabled(item))
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, "That entry is disabled.");

		var line = Line(window, item)
			?? throw UiErrors.Fail(
				UiErrorCodes.NotCreated,
				"That entry is not on screen: the flyout is closed or does not list it.");

		return MauiInputTargetResolver.Locate(window, line, context?.Node, context?.Revisions, "entry");
	}

	// The line of the flyout an entry is drawn as. An entry that is the only one inside the entry above it is
	// listed as that one, and a click there is a click on it.
	private static WinElement Line(Window window, BaseShellItem item)
	{
		for (Element current = item; current is BaseShellItem entry; current = current.Parent)
		{
			if (Find(window, entry) is { } line)
				return line;

			if (current.Parent is not BaseShellItem parent || MauiNodeBinder.Children(parent).Count() != 1)
				return null;
		}

		return null;
	}

	private static WinElement Find(Window window, BaseShellItem entry)
	{
		if (MauiPlatform.RootOf(window) is not { } root)
			return null;

		return MauiPlatform
			.PlatformDescendants(root)
			.OfType<WinNavigationItem>()
			.FirstOrDefault(line => MauiPlatform.IsDrawn(line) && Shows(line.DataContext, entry));
	}

	// The flyout keeps the entry a line shows in what the line is bound to: the entry itself, or a small
	// model of MAUI's own that carries it as its data.
	private static bool Shows(object context, BaseShellItem entry)
	{
		if (context is null)
			return false;

		if (ReferenceEquals(context, entry))
			return true;

		return context.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(context) is { } data
			&& ReferenceEquals(data, entry);
	}
}
