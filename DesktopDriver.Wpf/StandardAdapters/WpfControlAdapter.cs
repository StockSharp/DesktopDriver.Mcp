namespace StockSharp.DesktopDriver.Wpf;

using System;
using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Reads any WPF control: its text, its value, whether it is selected, checked, read-only or open.
/// </summary>
/// <remarks>
/// Registered as the fallback, at the lowest priority, so that any adapter written for a particular
/// control wins over it. A control nobody has written an adapter for still answers what every control
/// can answer, which is more useful than refusing to describe it.
/// <para>
/// A control that has no notion of one of these answers that it is unsupported rather than answering
/// <see langword="false"/>: "this button is not checked" and "a button has no checked state" are
/// different claims, and only one of them is true.
/// </para>
/// </remarks>
public class WpfControlAdapter(WpfNodeBinder binder) : IUiSnapshotAdapter, IUiContainerAdapter
{
	private readonly WpfNodeBinder _binder = binder ?? throw new ArgumentNullException(nameof(binder));

	/// <inheritdoc />
	public virtual string Kind => "control";

	/// <inheritdoc />
	public virtual bool CanHandle(UiSubject subject) => subject?.Instance is FrameworkElement;

	/// <inheritdoc />
	public virtual ImmutableArray<string> GetCapabilities(UiSubject subject)
	{
		var capabilities = ImmutableArray.CreateBuilder<string>();

		capabilities.Add("input.click");
		capabilities.Add("input.key");

		if (subject?.Instance is TextBox)
			capabilities.Add("input.text");

		if (subject?.Instance is FrameworkElement control && VisualTreeHelper.GetChildrenCount(control) > 0)
			capabilities.Add("tree.children");

		return capabilities.ToImmutable();
	}

	/// <inheritdoc />
	public virtual UiStateCapture CaptureState(UiSubject subject, UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var control = (FrameworkElement)subject.Instance;

		// A list is asked how much it is showing, the way a table and a tree are. Left as a basic state
		// it answers nothing at all, so a catalogue that has not arrived reads the same as one that has.
		if (control is ItemsControl items and not (TreeView or TabControl or MenuBase))
			return Listed(items);

		return new UiStateCapture(
			new BasicState(
				ReadText(control),
				ReadValue(control),
				ReadSelected(control),
				ReadChecked(control),
				ReadReadOnly(control),
				ReadExpanded(control),
				ReadSelectedItem(control),
				ReadActiveTab(control),
				ImmutableArray<string>.Empty),
			UiReadyStatuses.Ready,
			UiCompleteness.Complete,
			ImmutableArray<string>.Empty);
	}

	/// <inheritdoc />
	public virtual UiDataPage<UiChildLink> ReadChildren(UiSubject subject, UiPageRequest query, UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);
		ArgumentNullException.ThrowIfNull(query);

		var control = (FrameworkElement)subject.Instance;
		var links = ImmutableArray.CreateBuilder<UiChildLink>();
		var order = 0;
		var truncated = false;

		foreach (var child in _binder.GetSemanticChildren(control, includeVisualInternals: false))
		{
			if (links.Count >= query.Limit)
			{
				truncated = true;
				break;
			}

			var reference = _binder.Bind(child);

			if (reference is null)
				continue;

			links.Add(new UiChildLink(subject.Id, reference, UiRelations.Child, order++));
		}

		return new UiDataPage<UiChildLink>(
			context?.Node,
			null,
			links.ToImmutable(),
			UiField<long>.Known(links.Count),
			truncated,
			null,
			truncated ? "budget" : null);
	}

	// Counted from what the list was given rather than from what it has laid out: a long list builds a
	// windowful of items and recycles the rest as the reader scrolls.
	private static UiStateCapture Listed(ItemsControl items)
	{
		var selector = items as Selector;
		var list = items as ListBox;

		return new UiStateCapture(
			new ListState(
				UiField<long>.Known(items.Items.Count),
				selector is null
					? UiField<long>.Unavailable(UiUnavailableReasons.Unsupported, "not selectable")
					: UiField<long>.Known(selector.SelectedIndex),
				selector is null
					? UiField<long>.Unavailable(UiUnavailableReasons.Unsupported, "not selectable")
					: UiField<long>.Known(list?.SelectedItems?.Count ?? (selector.SelectedIndex >= 0 ? 1 : 0)),
				list is not null && list.SelectionMode != SelectionMode.Single),
			UiReadyStatuses.Ready,
			UiCompleteness.Complete,
			ImmutableArray<string>.Empty);
	}

	private static UiField<string> ReadText(FrameworkElement control)
		=> control switch
		{
			TextBox box => UiField<string>.Known(box.Text ?? string.Empty),
			TextBlock block => UiField<string>.Known(block.Text ?? string.Empty),
			Window window => UiField<string>.Known(window.Title ?? string.Empty),
			ContentControl content => content.Content is string text
				? UiField<string>.Known(text)
				: UiField<string>.Known(content.Content?.ToString() ?? string.Empty),
			_ => UiField<string>.Unavailable(UiUnavailableReasons.Unsupported, "no text"),
		};

	private static UiField<UiValue> ReadValue(FrameworkElement control)
		=> control switch
		{
			TextBox box => UiField<UiValue>.Known(new UiStringValue(box.Text ?? string.Empty)),
			RangeBase range => UiField<UiValue>.Known(new UiDoubleValue(range.Value)),
			ToggleButton toggle => UiField<UiValue>.Known(toggle.IsChecked is bool flag
				? new UiBooleanValue(flag)
				: UiNullValue.Instance),
			_ => UiField<UiValue>.Unavailable(UiUnavailableReasons.Unsupported, "no value"),
		};

	private static UiField<bool> ReadSelected(FrameworkElement control)
		=> control switch
		{
			ListBoxItem item => UiField<bool>.Known(item.IsSelected),
			TabItem tab => UiField<bool>.Known(tab.IsSelected),
			_ => UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "not selectable"),
		};

	// Three states, and the third one is a value: a checkbox that is deliberately indeterminate is not
	// a checkbox nobody could read.
	private static UiField<bool?> ReadChecked(FrameworkElement control)
		=> control is ToggleButton toggle
			? UiField<bool?>.Known(toggle.IsChecked)
			: UiField<bool?>.Unavailable(UiUnavailableReasons.Unsupported, "not a toggle");

	private static UiField<bool> ReadReadOnly(FrameworkElement control)
		=> control switch
		{
			TextBox box => UiField<bool>.Known(box.IsReadOnly),
			_ => UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "not editable"),
		};

	private static UiField<bool> ReadExpanded(FrameworkElement control)
		=> control switch
		{
			Expander expander => UiField<bool>.Known(expander.IsExpanded),
			ComboBox combo => UiField<bool>.Known(combo.IsDropDownOpen),
			TreeViewItem item => UiField<bool>.Known(item.IsExpanded),
			_ => UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "does not expand"),
		};

	private UiField<UiNodeId> ReadSelectedItem(FrameworkElement control)
	{
		if (control is not Selector selector)
			return UiField<UiNodeId>.Unavailable(UiUnavailableReasons.Unsupported, "no selection");

		if (selector.SelectedIndex < 0)
			return UiField<UiNodeId>.Known(null);

		var container = selector.ItemContainerGenerator.ContainerFromIndex(selector.SelectedIndex);

		return container is not FrameworkElement item
			? UiField<UiNodeId>.Unavailable(UiUnavailableReasons.NotCreated, "the selected item has no visual")
			: UiField<UiNodeId>.Known(_binder.Bind(item)?.Id);
	}

	private UiField<UiNodeId> ReadActiveTab(FrameworkElement control)
	{
		if (control is not TabControl tabs)
			return UiField<UiNodeId>.Unavailable(UiUnavailableReasons.Unsupported, "not a tab strip");

		if (tabs.SelectedIndex < 0)
			return UiField<UiNodeId>.Known(null);

		var container = tabs.ItemContainerGenerator.ContainerFromIndex(tabs.SelectedIndex);

		return container is not FrameworkElement tab
			? UiField<UiNodeId>.Unavailable(UiUnavailableReasons.NotCreated, "the selected tab has no visual")
			: UiField<UiNodeId>.Known(_binder.Bind(tab)?.Id);
	}
}
