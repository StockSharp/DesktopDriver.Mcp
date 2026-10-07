namespace StockSharp.DesktopDriver.Avalonia;

using System;
using System.Collections.Immutable;
using System.Linq;

using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.VisualTree;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Reads any Avalonia control: its text, its value, whether it is selected, checked, read-only or open.
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
public class AvaloniaControlAdapter(AvaloniaNodeBinder binder) : IUiSnapshotAdapter, IUiContainerAdapter
{
	private readonly AvaloniaNodeBinder _binder = binder ?? throw new ArgumentNullException(nameof(binder));

	/// <inheritdoc />
	public virtual string Kind => "control";

	/// <inheritdoc />
	public virtual bool CanHandle(UiSubject subject) => subject?.Instance is Control;

	/// <inheritdoc />
	public virtual ImmutableArray<string> GetCapabilities(UiSubject subject)
	{
		var capabilities = ImmutableArray.CreateBuilder<string>();

		capabilities.Add("input.click");
		capabilities.Add("input.key");

		if (subject?.Instance is TextBox)
			capabilities.Add("input.text");

		if (subject?.Instance is Control control && control.GetVisualChildren().Any())
			capabilities.Add("tree.children");

		return capabilities.ToImmutable();
	}

	/// <inheritdoc />
	public virtual UiStateCapture CaptureState(UiSubject subject, UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var control = (Control)subject.Instance;

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

		var control = (Control)subject.Instance;
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
		var selecting = items as SelectingItemsControl;
		var list = items as ListBox;

		return new UiStateCapture(
			new ListState(
				UiField<long>.Known(items.ItemCount),
				selecting is null
					? UiField<long>.Unavailable(UiUnavailableReasons.Unsupported, "not selectable")
					: UiField<long>.Known(selecting.SelectedIndex),
				selecting is null
					? UiField<long>.Unavailable(UiUnavailableReasons.Unsupported, "not selectable")
					: UiField<long>.Known(list?.SelectedItems?.Count ?? (selecting.SelectedIndex >= 0 ? 1 : 0)),
				list?.SelectionMode.HasFlag(SelectionMode.Multiple) == true),
			UiReadyStatuses.Ready,
			UiCompleteness.Complete,
			ImmutableArray<string>.Empty);
	}

	private static UiField<string> ReadText(Control control)
		=> control switch
		{
			TextBox box => UiField<string>.Known(box.Text ?? string.Empty),
			TextBlock block => UiField<string>.Known(block.Text ?? string.Empty),
			Window window => UiField<string>.Known(window.Title ?? string.Empty),
			ContentControl content => content.Content is string text
				? UiField<string>.Known(text)
				: UiField<string>.Known(CaptionOf(content)),
			_ => UiField<string>.Unavailable(UiUnavailableReasons.Unsupported, "no text"),
		};

	// Content that is drawn - a logo beside a name - has no text of its own; the name given to it for people who
	// cannot see it is what it says.
	private static string CaptionOf(ContentControl content)
		=> AutomationProperties.GetName(content) is { Length: > 0 } name
			? name
			: content.Content?.ToString() ?? string.Empty;

	private static UiField<UiValue> ReadValue(Control control)
		=> control switch
		{
			TextBox box => UiField<UiValue>.Known(new UiStringValue(box.Text ?? string.Empty)),
			RangeBase range => UiField<UiValue>.Known(new UiDoubleValue(range.Value)),
			ToggleButton toggle => UiField<UiValue>.Known(toggle.IsChecked is bool flag
				? new UiBooleanValue(flag)
				: UiNullValue.Instance),
			_ => UiField<UiValue>.Unavailable(UiUnavailableReasons.Unsupported, "no value"),
		};

	private static UiField<bool> ReadSelected(Control control)
		=> control switch
		{
			ListBoxItem item => UiField<bool>.Known(item.IsSelected),
			TabItem tab => UiField<bool>.Known(tab.IsSelected),
			_ => UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "not selectable"),
		};

	// Three states, and the third one is a value: a checkbox that is deliberately indeterminate is not
	// a checkbox nobody could read.
	private static UiField<bool?> ReadChecked(Control control)
		=> control is ToggleButton toggle
			? UiField<bool?>.Known(toggle.IsChecked)
			: UiField<bool?>.Unavailable(UiUnavailableReasons.Unsupported, "not a toggle");

	private static UiField<bool> ReadReadOnly(Control control)
		=> control switch
		{
			TextBox box => UiField<bool>.Known(box.IsReadOnly),
			_ => UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "not editable"),
		};

	private static UiField<bool> ReadExpanded(Control control)
		=> control switch
		{
			Expander expander => UiField<bool>.Known(expander.IsExpanded),
			ComboBox combo => UiField<bool>.Known(combo.IsDropDownOpen),
			TreeViewItem item => UiField<bool>.Known(item.IsExpanded),
			_ => UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "does not expand"),
		};

	private UiField<UiNodeId> ReadSelectedItem(Control control)
	{
		if (control is not SelectingItemsControl selector)
			return UiField<UiNodeId>.Unavailable(UiUnavailableReasons.Unsupported, "no selection");

		if (selector.SelectedIndex < 0)
			return UiField<UiNodeId>.Known(null);

		var container = selector.ContainerFromIndex(selector.SelectedIndex);

		return container is null
			? UiField<UiNodeId>.Unavailable(UiUnavailableReasons.NotCreated, "the selected item has no visual")
			: UiField<UiNodeId>.Known(_binder.Bind(container)?.Id);
	}

	private UiField<UiNodeId> ReadActiveTab(Control control)
	{
		if (control is not TabControl tabs)
			return UiField<UiNodeId>.Unavailable(UiUnavailableReasons.Unsupported, "not a tab strip");

		if (tabs.SelectedIndex < 0)
			return UiField<UiNodeId>.Known(null);

		var container = tabs.ContainerFromIndex(tabs.SelectedIndex);

		return container is null
			? UiField<UiNodeId>.Unavailable(UiUnavailableReasons.NotCreated, "the selected tab has no visual")
			: UiField<UiNodeId>.Known(_binder.Bind(container)?.Id);
	}
}
