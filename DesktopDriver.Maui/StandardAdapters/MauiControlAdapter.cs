namespace StockSharp.DesktopDriver.Maui;

using System;
using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Microsoft.Maui.Controls;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Snapshots;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;

/// <summary>
/// Reads any MAUI element: what it says, the value it holds, what it has chosen, and what is inside it.
/// </summary>
/// <param name="binder">Registers the elements it reports.</param>
public class MauiControlAdapter(MauiNodeBinder binder) : IUiSnapshotAdapter, IUiContainerAdapter
{
	/// <summary>
	/// Registers the elements this adapter reports.
	/// </summary>
	protected MauiNodeBinder Binder { get; } = binder ?? throw new ArgumentNullException(nameof(binder));

	/// <inheritdoc />
	public virtual string Kind => "control";

	/// <inheritdoc />
	public virtual bool CanHandle(UiSubject subject) => subject?.Instance is Element;

	/// <inheritdoc />
	public virtual ImmutableArray<string> GetCapabilities(UiSubject subject)
	{
		var capabilities = ImmutableArray.CreateBuilder<string>();

		capabilities.Add("input.click");

		if (subject?.Instance is InputView)
		{
			capabilities.Add("input.text");
			capabilities.Add("input.key");
		}

		if (subject?.Instance is Element element && MauiNodeBinder.Children(element).Any())
			capabilities.Add("tree.children");

		return capabilities.ToImmutable();
	}

	/// <inheritdoc />
	public virtual UiStateCapture CaptureState(UiSubject subject, UiCaptureContext context)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var element = (Element)subject.Instance;

		// A list is asked how much it is showing. Left as a basic state it answers nothing at all, so a list
		// whose rows have not arrived reads the same as one whose rows have.
		if (element is ItemsView list)
			return Listed(list);

		return new UiStateCapture(
			new BasicState(
				ReadText(element),
				ReadValue(element),
				ReadSelected(element),
				ReadChecked(element),
				ReadReadOnly(element),
				UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "does not expand"),
				ReadSelectedItem(element),
				ReadActiveTab(element),
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

		var element = (Element)subject.Instance;
		var links = ImmutableArray.CreateBuilder<UiChildLink>();
		var order = 0;
		var truncated = false;

		foreach (var child in Binder.GetSemanticChildren(element, includeVisualInternals: false))
		{
			if (links.Count >= query.Limit)
			{
				truncated = true;
				break;
			}

			var reference = Binder.Bind(child);

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

	/// <summary>
	/// The values a list was given, in its order.
	/// </summary>
	/// <param name="element">The list.</param>
	/// <returns>The values, or <see langword="null"/> when it was given none.</returns>
	internal static IList ItemsOf(ItemsView element)
	{
		var source = element.ItemsSource;

		return source switch
		{
			null => null,
			IList list => list,
			_ => source.Cast<object>().ToList(),
		};
	}

	// Counted from what the list was given rather than from what it has laid out: a long list draws a
	// windowful of rows and recycles them as the reader scrolls.
	private static UiStateCapture Listed(ItemsView element)
	{
		var items = ItemsOf(element);
		var count = items?.Count ?? 0;

		var (selected, chosen, many) = element switch
		{
			SelectableItemsView selectable => (
				Index(items, selectable.SelectedItem),
				selectable.SelectionMode == SelectionMode.Multiple
					? selectable.SelectedItems?.Count ?? 0
					: selectable.SelectedItem is null ? 0 : 1,
				selectable.SelectionMode == SelectionMode.Multiple),
			_ => (-1, 0, false),
		};

		var choosable = element is SelectableItemsView;

		return new UiStateCapture(
			new ListState(
				UiField<long>.Known(count),
				choosable
					? UiField<long>.Known(selected)
					: UiField<long>.Unavailable(UiUnavailableReasons.Unsupported, "not selectable"),
				choosable
					? UiField<long>.Known(chosen)
					: UiField<long>.Unavailable(UiUnavailableReasons.Unsupported, "not selectable"),
				many),
			UiReadyStatuses.Ready,
			UiCompleteness.Complete,
			ImmutableArray<string>.Empty);
	}

	private static int Index(IList items, object value)
		=> items is null || value is null ? -1 : items.IndexOf(value);

	private static UiField<string> ReadText(Element element)
		=> element switch
		{
			Label label => UiField<string>.Known(label.Text ?? label.FormattedText?.ToString() ?? string.Empty),
			Button button => UiField<string>.Known(button.Text ?? string.Empty),
			InputView input => UiField<string>.Known(input.Text ?? string.Empty),
			Picker picker => UiField<string>.Known(
				picker.SelectedIndex >= 0 && picker.SelectedIndex < picker.Items.Count
					? picker.Items[picker.SelectedIndex]
					: string.Empty),
			DatePicker date => UiField<string>.Known(
				date.Date is DateTime day ? day.ToString(date.Format, CultureInfo.CurrentCulture) : string.Empty),
			TimePicker time => UiField<string>.Known(
				time.Time is TimeSpan moment ? DateTime.Today.Add(moment).ToString(time.Format, CultureInfo.CurrentCulture) : string.Empty),
			RadioButton radio => UiField<string>.Known(radio.Content as string ?? radio.Content?.ToString() ?? string.Empty),
			Page page => UiField<string>.Known(page.Title ?? string.Empty),
			Window window => UiField<string>.Known(window.Title ?? string.Empty),
			BaseShellItem item => UiField<string>.Known(item.Title ?? string.Empty),
			_ => UiField<string>.Unavailable(UiUnavailableReasons.Unsupported, "no text"),
		};

	private static UiField<UiValue> ReadValue(Element element)
		=> element switch
		{
			InputView input => UiField<UiValue>.Known(new UiStringValue(input.Text ?? string.Empty)),
			Slider slider => UiField<UiValue>.Known(new UiDoubleValue(slider.Value)),
			Stepper stepper => UiField<UiValue>.Known(new UiDoubleValue(stepper.Value)),
			ProgressBar progress => UiField<UiValue>.Known(new UiDoubleValue(progress.Progress)),
			// Whether it is turning: a page that waits for an answer says so with one, and a wait for the page
			// to be done is a wait for this to stop.
			ActivityIndicator busy => UiField<UiValue>.Known(new UiBooleanValue(busy.IsRunning)),
			CheckBox check => UiField<UiValue>.Known(new UiBooleanValue(check.IsChecked)),
			Switch toggle => UiField<UiValue>.Known(new UiBooleanValue(toggle.IsToggled)),
			RadioButton radio => UiField<UiValue>.Known(new UiBooleanValue(radio.IsChecked)),
			// A day, with nothing of the clock in it - so the zone it is read in cannot move it to another day.
			DatePicker date => UiField<UiValue>.Known(date.Date is DateTime day
				? new UiTimestampValue(DateTime.SpecifyKind(day.Date, DateTimeKind.Utc))
				: UiNullValue.Instance),
			_ => UiField<UiValue>.Unavailable(UiUnavailableReasons.Unsupported, "no value"),
		};

	// A shell entry is selected while it is the one shown, which is what selecting a tab means anywhere else.
	private static UiField<bool> ReadSelected(Element element)
		=> element switch
		{
			ShellItem item when item.Parent is Shell shell => UiField<bool>.Known(ReferenceEquals(shell.CurrentItem, item)),
			ShellSection section when section.Parent is ShellItem item => UiField<bool>.Known(ReferenceEquals(item.CurrentItem, section)),
			ShellContent content when content.Parent is ShellSection section => UiField<bool>.Known(ReferenceEquals(section.CurrentItem, content)),
			_ => UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "not selectable"),
		};

	private static UiField<bool?> ReadChecked(Element element)
		=> element switch
		{
			CheckBox check => UiField<bool?>.Known(check.IsChecked),
			Switch toggle => UiField<bool?>.Known(toggle.IsToggled),
			RadioButton radio => UiField<bool?>.Known(radio.IsChecked),
			_ => UiField<bool?>.Unavailable(UiUnavailableReasons.Unsupported, "not a toggle"),
		};

	private static UiField<bool> ReadReadOnly(Element element)
		=> element is InputView input
			? UiField<bool>.Known(input.IsReadOnly)
			: UiField<bool>.Unavailable(UiUnavailableReasons.Unsupported, "not editable");

	private UiField<UiNodeId> ReadSelectedItem(Element element)
	{
		if (element is not SelectableItemsView list)
			return UiField<UiNodeId>.Unavailable(UiUnavailableReasons.Unsupported, "no selection");

		if (list.SelectedItem is null)
			return UiField<UiNodeId>.Known(null);

		var row = MauiNodeBinder.Children(list).FirstOrDefault(child => Equals(child.BindingContext, list.SelectedItem));

		return row is null
			? UiField<UiNodeId>.Unavailable(UiUnavailableReasons.NotCreated, "the selected item is not drawn")
			: UiField<UiNodeId>.Known(Binder.Bind(row)?.Id);
	}

	private UiField<UiNodeId> ReadActiveTab(Element element)
	{
		Element current = element switch
		{
			Shell shell => shell.CurrentItem,
			ShellItem item => item.CurrentItem,
			ShellSection section => section.CurrentItem,
			TabbedPage tabs => tabs.CurrentPage,
			_ => null,
		};

		if (element is not (Shell or ShellItem or ShellSection or TabbedPage))
			return UiField<UiNodeId>.Unavailable(UiUnavailableReasons.Unsupported, "not a tab strip");

		return UiField<UiNodeId>.Known(current is null ? null : Binder.Bind(current)?.Id);
	}
}
