namespace StockSharp.DesktopDriver.Maui;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.Maui.Controls;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Waiting;

/// <summary>
/// Moves a node's revisions when the element behind it changes.
/// </summary>
/// <param name="revisions">Where a change is reported.</param>
internal sealed class MauiRevisionWatcher(IUiRevisionSink revisions) : IDisposable
{
	private static readonly Dictionary<string, UiRevisionKinds> _properties = new()
	{
		[nameof(Label.Text)] = UiRevisionKinds.State,
		[nameof(Label.FormattedText)] = UiRevisionKinds.State,
		[nameof(CheckBox.IsChecked)] = UiRevisionKinds.State,
		[nameof(Switch.IsToggled)] = UiRevisionKinds.State,
		[nameof(Slider.Value)] = UiRevisionKinds.State,
		[nameof(ProgressBar.Progress)] = UiRevisionKinds.State,
		[nameof(ActivityIndicator.IsRunning)] = UiRevisionKinds.State,
		[nameof(DatePicker.Date)] = UiRevisionKinds.State,
		[nameof(TimePicker.Time)] = UiRevisionKinds.State,
		[nameof(Page.Title)] = UiRevisionKinds.State,
		[nameof(ContentView.Content)] = UiRevisionKinds.State,
		[nameof(ItemsView.ItemsSource)] = UiRevisionKinds.State,
		[nameof(Window.Page)] = UiRevisionKinds.State,

		[nameof(Picker.SelectedIndex)] = UiRevisionKinds.View,
		[nameof(Picker.SelectedItem)] = UiRevisionKinds.View,
		[nameof(Shell.CurrentItem)] = UiRevisionKinds.View,
		[nameof(TabbedPage.CurrentPage)] = UiRevisionKinds.View,
		[nameof(VisualElement.IsVisible)] = UiRevisionKinds.View,
		[nameof(VisualElement.IsEnabled)] = UiRevisionKinds.View,
		[nameof(VisualElement.IsFocused)] = UiRevisionKinds.View,

		[nameof(VisualElement.Width)] = UiRevisionKinds.Layout,
		[nameof(VisualElement.Height)] = UiRevisionKinds.Layout,
		[nameof(VisualElement.X)] = UiRevisionKinds.Layout,
		[nameof(VisualElement.Y)] = UiRevisionKinds.Layout,
	};

	private readonly IUiRevisionSink _revisions = revisions ?? throw new ArgumentNullException(nameof(revisions));

	// Weak on the element: a shell builds a page again each time it is navigated to, and a table that held
	// every page ever read would keep all of them alive for as long as the product runs.
	private readonly ConditionalWeakTable<Element, Subscription> _watched = [];
	private readonly Lock _sync = new();

	private bool _disposed;

	/// <summary>
	/// What each kind of revision follows, for a reader that wants to know what a wait can see.
	/// </summary>
	public static IReadOnlyDictionary<UiRevisionKinds, string[]> Watched { get; } =
		_properties
			.GroupBy(pair => pair.Value)
			.ToDictionary(group => group.Key, group => group.Select(pair => pair.Key).Order().ToArray());

	/// <summary>
	/// Starts following an element.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <param name="id">The node it is.</param>
	public void Watch(Element element, UiNodeId id)
	{
		if (element is null || id is null)
			return;

		using (_sync.EnterScope())
		{
			if (_disposed || _watched.TryGetValue(element, out _))
				return;

			var watch = new Subscription(_revisions, id, element);

			_watched.Add(element, watch);

			watch.Attach();
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Subscription[] watched;

		using (_sync.EnterScope())
		{
			if (_disposed)
				return;

			_disposed = true;

			watched = [.. _watched.Select(pair => pair.Value)];

			_watched.Clear();
		}

		// Taken off on the thread that owns the elements, which is the only one that may touch them. Asked of the
		// application rather than of each element: an element made where there is no such thread has no answer
		// of its own and throws when asked.
		if (Application.Current?.Dispatcher is { IsDispatchRequired: true } dispatcher)
		{
			dispatcher.Dispatch(() => Detach(watched));
			return;
		}

		Detach(watched);
	}

	private static void Detach(Subscription[] watched)
	{
		foreach (var watch in watched)
			watch.Detach();
	}

	private sealed class Subscription(IUiRevisionSink revisions, UiNodeId id, Element element)
	{
		private INotifyCollectionChanged _items;

		public Element Element { get; } = element;

		public void Attach()
		{
			Element.PropertyChanged += OnPropertyChanged;

			if (Element is VisualElement visual)
			{
				visual.Loaded += OnShownOrHidden;
				visual.Unloaded += OnShownOrHidden;
			}

			if (Element is Window window)
			{
				window.ModalPushed += OnModalChanged;
				window.ModalPopped += OnModalChanged;
			}

			Follow(ItemsOf(Element));
		}

		public void Detach()
		{
			Element.PropertyChanged -= OnPropertyChanged;

			if (Element is VisualElement visual)
			{
				visual.Loaded -= OnShownOrHidden;
				visual.Unloaded -= OnShownOrHidden;
			}

			if (Element is Window window)
			{
				window.ModalPushed -= OnModalChanged;
				window.ModalPopped -= OnModalChanged;
			}

			Follow(null);
		}

		private static INotifyCollectionChanged ItemsOf(Element element)
			=> element switch
			{
				ItemsView items => items.ItemsSource as INotifyCollectionChanged,
				Picker picker => picker.ItemsSource as INotifyCollectionChanged,
				_ => null,
			};

		private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is null || !_properties.TryGetValue(e.PropertyName, out var kind))
				return;

			// A list given a new source raises nothing more for what is added to that source later, so the
			// one it is following moves with it.
			if (e.PropertyName == nameof(ItemsView.ItemsSource))
				Follow(ItemsOf(Element));

			revisions.Bump(id, kind);
		}

		private void OnShownOrHidden(object sender, EventArgs e) => revisions.Bump(id, UiRevisionKinds.View);

		private void OnModalChanged(object sender, ModalEventArgs e) => revisions.Bump(id, UiRevisionKinds.State);

		// An element's own properties are not the only way its data moves: a list whose rows arrive through
		// the collection it was given raises no property change at all.
		private void Follow(INotifyCollectionChanged source)
		{
			if (ReferenceEquals(_items, source))
				return;

			if (_items is not null)
				_items.CollectionChanged -= OnItemsChanged;

			_items = source;

			if (_items is not null)
				_items.CollectionChanged += OnItemsChanged;
		}

		private void OnItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
			=> revisions.Bump(id, UiRevisionKinds.State);
	}
}
