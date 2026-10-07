namespace StockSharp.DesktopDriver.Wpf;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Waiting;

/// <summary>
/// Watches a real control and says when it moved.
/// </summary>
/// <remarks>
/// This is what makes a read guard and a wait mean anything. A guard says "answer only if this is
/// still the state I saw", and a wait says "tell me when it is not" - both compare revisions, and a
/// revision that never moves turns the first into "always yes" and the second into "never".
/// <para>
/// What is watched is written out in <see cref="Watched"/> rather than left to be inferred, because a
/// caller has to know which changes it will hear about. Anything not in that list is not reported.
/// </para>
/// <para>
/// A dependency property is heard through a descriptor, one per property per element, and a descriptor
/// keeps hold of the element for as long as the handler is attached. So this class owns everything it
/// subscribed and <see cref="Dispose"/> is what lets the elements go again.
/// </para>
/// </remarks>
internal sealed class WpfRevisionWatcher(IUiRevisionSink revisions) : IDisposable
{
	private static readonly (DependencyProperty Property, Type Owner, UiRevisionKinds Kind)[] _properties =
	[
		(TextBox.TextProperty, typeof(TextBox), UiRevisionKinds.State),
		(ToggleButton.IsCheckedProperty, typeof(ToggleButton), UiRevisionKinds.State),
		(ContentControl.ContentProperty, typeof(ContentControl), UiRevisionKinds.State),
		(ItemsControl.ItemsSourceProperty, typeof(ItemsControl), UiRevisionKinds.State),

		(Selector.SelectedIndexProperty, typeof(Selector), UiRevisionKinds.View),
		(TreeViewItem.IsExpandedProperty, typeof(TreeViewItem), UiRevisionKinds.View),
		(Expander.IsExpandedProperty, typeof(Expander), UiRevisionKinds.View),
		(UIElement.IsVisibleProperty, typeof(UIElement), UiRevisionKinds.View),
		(UIElement.IsEnabledProperty, typeof(UIElement), UiRevisionKinds.View),

		(FrameworkElement.ActualWidthProperty, typeof(FrameworkElement), UiRevisionKinds.Layout),
		(FrameworkElement.ActualHeightProperty, typeof(FrameworkElement), UiRevisionKinds.Layout),
	];

	private readonly IUiRevisionSink _revisions = revisions ?? throw new ArgumentNullException(nameof(revisions));
	private readonly Dictionary<FrameworkElement, Subscription> _watched = [];
	private readonly Lock _sync = new();

	private bool _disposed;

	/// <summary>
	/// What a caller will hear about, by revision.
	/// </summary>
	/// <remarks>
	/// Layout is the size an element ended up with: an element that only moves inside its parent changes
	/// none of its own properties, so a move without a resize is not reported.
	/// </remarks>
	public static IReadOnlyDictionary<UiRevisionKinds, string[]> Watched { get; } =
		new Dictionary<UiRevisionKinds, string[]>
		{
			[UiRevisionKinds.State] =
			[
				"TextBox.Text", "ToggleButton.IsChecked", "ContentControl.Content",
				"ItemsControl.ItemsSource",
				"the items themselves, through ItemsControl.Items, whether they arrive from a source or were added directly",
			],
			[UiRevisionKinds.View] =
			[
				"Selector.SelectedIndex", "TreeViewItem.IsExpanded", "Expander.IsExpanded",
				"UIElement.IsVisible", "UIElement.IsEnabled",
			],
			[UiRevisionKinds.Layout] = ["FrameworkElement.ActualWidth", "FrameworkElement.ActualHeight"],
		};

	/// <summary>
	/// Starts watching an element, once.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <param name="id">The address it answers to.</param>
	/// <remarks>
	/// Binding the same element twice subscribes once: the table remembers which elements are already
	/// watched. After <see cref="Dispose"/> nothing is watched any more and the call does nothing.
	/// </remarks>
	public void Watch(FrameworkElement element, UiNodeId id)
	{
		if (element is null || id is null)
			return;

		using (_sync.EnterScope())
		{
			if (_disposed || _watched.ContainsKey(element))
				return;

			var watch = new Subscription(_revisions, id, element);

			_watched.Add(element, watch);

			watch.Attach();

			if (element is ItemsControl items)
				watch.Follow(items.Items as INotifyCollectionChanged);
		}
	}

	/// <summary>
	/// Stops watching everything, and releases every element this watcher held.
	/// </summary>
	public void Dispose()
	{
		Subscription[] watched;

		using (_sync.EnterScope())
		{
			if (_disposed)
				return;

			_disposed = true;

			watched = [.. _watched.Values];

			_watched.Clear();
		}

		foreach (var watch in watched)
		{
			// A handler left attached is what keeps the element alive: the descriptor that carries it holds
			// the element in a table of its own that nothing ever sweeps, so an element still subscribed
			// outlives the window it was in and every row it was showing.
			var dispatcher = watch.Element.Dispatcher;

			// Removed on the thread that owns the element: the descriptor listens through a binding, and a
			// binding is only touched from there.
			if (dispatcher.CheckAccess())
				watch.Detach();
			else if (!dispatcher.HasShutdownStarted)
				dispatcher.InvokeAsync(watch.Detach);
		}
	}

	private sealed class Subscription(IUiRevisionSink revisions, UiNodeId id, FrameworkElement element)
	{
		private readonly List<(DependencyPropertyDescriptor Descriptor, EventHandler Handler)> _handlers = [];

		private INotifyCollectionChanged _items;

		public FrameworkElement Element { get; } = element;

		public void Attach()
		{
			foreach (var (property, owner, kind) in _properties)
			{
				if (!owner.IsInstanceOfType(Element))
					continue;

				var descriptor = DependencyPropertyDescriptor.FromProperty(property, owner);

				if (descriptor is null)
					continue;

				// The handler is kept, not rebuilt: removal matches on the delegate instance, and one built
				// a second time would not take the first one off.
				var handler = new EventHandler((_, _) => revisions.Bump(id, kind));

				descriptor.AddValueChanged(Element, handler);

				_handlers.Add((descriptor, handler));
			}
		}

		public void Detach()
		{
			foreach (var (descriptor, handler) in _handlers)
				descriptor.RemoveValueChanged(Element, handler);

			_handlers.Clear();

			Follow(null);
		}

		// An element's own properties are not the only way its data moves: a grid whose rows arrive
		// through the collection it was given raises no property change at all.
		public void Follow(INotifyCollectionChanged source)
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
