namespace StockSharp.DesktopDriver.Maui;

using System;
using System.Linq;

using Microsoft.Maui.Controls;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Reads a list and says where a row of it is, so that a row can be pointed at by its place in the list.
/// </summary>
/// <param name="binder">Registers the elements it reports.</param>
public sealed class MauiListAdapter(MauiNodeBinder binder)
	: MauiControlAdapter(binder), IUiInputTargetAdapter
{
	/// <inheritdoc />
	public override string Kind => "list";

	/// <inheritdoc />
	public override bool CanHandle(UiSubject subject) => subject?.Instance is ItemsView;

	/// <inheritdoc />
	public bool SupportsTargetPart(UiSubject subject, UiTargetPart part)
		=> CanHandle(subject) && part is UiListItemPart;

	/// <inheritdoc />
	public UiResolvedInputTarget ResolveInputTarget(
		UiSubject subject,
		UiTargetPart part,
		UiInputAction action,
		UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		if (part is not UiListItemPart item)
			throw UiErrors.Unsupported($"A list has no part called {part?.Kind}.");

		var list = (ItemsView)subject.Instance;
		var items = ItemsOf(list);
		var count = items?.Count ?? 0;

		if (item.Index < 0 || item.Index >= count)
		{
			throw UiErrors.Fail(
				UiErrorCodes.NotFound,
				$"This list is showing {count} items, so there is no item {item.Index}.");
		}

		var index = (int)item.Index;
		var value = items[index];

		// A row is drawn only while it is in view; one that is not has no place on screen to point at until
		// the list has scrolled to it.
		var row = Row(list, value);

		if (row is null || MauiPlatform.ViewOf(row) is not { } view || !MauiPlatform.IsDrawn(view))
		{
			list.ScrollTo(index, position: ScrollToPosition.MakeVisible, animate: false);

			throw UiErrors.Fail(
				UiErrorCodes.NotCreated,
				$"Item {item.Index} is not drawn: the list has been scrolled to it, and it can be asked for again once it is.");
		}

		var window = UiAutomationNames.GetWindow(list)
			?? throw UiErrors.Fail(UiErrorCodes.NotCreated, "That list is not in a window.");

		// Pointed at as the line the list draws around the row, which is what takes a click and is chosen by it.
		return MauiInputTargetResolver.Locate(window, MauiPlatform.ContainerOf(view), context?.Node, context?.Revisions, $"item {item.Index}");
	}

	// The element a row is drawn with is the one the row's value was handed to.
	private static Element Row(ItemsView list, object value)
		=> MauiNodeBinder.Children(list).FirstOrDefault(child => Equals(child.BindingContext, value));
}
