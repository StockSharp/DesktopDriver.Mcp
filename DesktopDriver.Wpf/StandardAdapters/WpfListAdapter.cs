namespace StockSharp.DesktopDriver.Wpf;

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Snapshots;

/// <summary>
/// Points at an item of a list.
/// </summary>
/// <remarks>
/// A list is the one place with nothing else to point at: its items are values rather than records, and
/// what a person does with one is point at the first, or the third.
/// <para>
/// A list builds only what it is showing and recycles the rest as the reader scrolls, so an item that is
/// out of view has no visual at all. It is scrolled to first, which is what a reader does.
/// </para>
/// </remarks>
public sealed class WpfListAdapter(WpfNodeBinder binder)
	: WpfControlAdapter(binder), IUiInputTargetAdapter
{
	/// <inheritdoc />
	public override string Kind => "list";

	/// <inheritdoc />
	public override bool CanHandle(UiSubject subject)
		=> subject?.Instance is ItemsControl and not (TreeView or TabControl or MenuBase);

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

		var list = (ItemsControl)subject.Instance;
		var count = list.Items.Count;

		if (item.Index < 0 || item.Index >= count)
		{
			throw UiErrors.Fail(
				UiErrorCodes.NotFound,
				$"This list is showing {count} items, so there is no item {item.Index}.");
		}

		var index = (int)item.Index;

		// Brought into view before the container is looked for, because until the list has scrolled there
		// is none: a value a hundred down has no place on screen at all.
		if (list is ListBox box)
			box.ScrollIntoView(list.Items[index]);

		list.UpdateLayout();

		var container = list.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement
			?? throw UiErrors.Fail(
				UiErrorCodes.NotCreated,
				$"Item {item.Index} is not drawn: the list has not laid that part of itself out yet.");

		return WpfInputTargetResolver.Locate(container, context?.Node, context?.Revisions, $"item {item.Index}");
	}
}
