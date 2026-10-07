namespace StockSharp.DesktopDriver.Avalonia;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;

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
/// </remarks>
internal sealed class AvaloniaRevisionWatcher(IUiRevisionSink revisions)
{
	private readonly IUiRevisionSink _revisions = revisions ?? throw new ArgumentNullException(nameof(revisions));
	private readonly ConditionalWeakTable<Control, Subscription> _watched = [];

	/// <summary>
	/// What a caller will hear about, by revision.
	/// </summary>
	public static IReadOnlyDictionary<UiRevisionKinds, string[]> Watched { get; } =
		new Dictionary<UiRevisionKinds, string[]>
		{
			[UiRevisionKinds.State] =
			[
				"TextBox.Text", "ToggleButton.IsChecked", "ContentControl.Content",
				"ItemsControl.ItemsSource", "ItemsControl.ItemCount",
				"the items themselves, when the source reports its own changes",
			],
			[UiRevisionKinds.View] =
			[
				"SelectingItemsControl.SelectedIndex", "TreeViewItem.IsExpanded", "Expander.IsExpanded",
				"Visual.IsVisible", "InputElement.IsEnabled",
			],
			[UiRevisionKinds.Layout] = ["Visual.Bounds"],
		};

	/// <summary>
	/// Starts watching a control, once.
	/// </summary>
	/// <param name="control">The control.</param>
	/// <param name="id">The address it answers to.</param>
	/// <remarks>
	/// Nothing here keeps the control alive: the handler is registered on the control itself and dies
	/// with it. The table only remembers which controls are already watched, so binding one twice does
	/// not subscribe twice.
	/// </remarks>
	public void Watch(Control control, UiNodeId id, IEnumerable<UiViewWatch> viewRules)
	{
		if (control is null || id is null || _watched.TryGetValue(control, out _))
			return;

		var watch = new Subscription(_revisions, id);

		_watched.Add(control, watch);

		control.PropertyChanged += watch.OnPropertyChanged;

		if (control is ItemsControl items)
			watch.Follow(items.ItemsSource as INotifyCollectionChanged);

		foreach (var rule in viewRules ?? [])
			rule(control, watch.ViewChanged);
	}

	private sealed class Subscription(IUiRevisionSink revisions, UiNodeId id)
	{
		private INotifyCollectionChanged _items;

		public void OnPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
		{
			var kind = KindOf(e.Property);

			if (kind is not null)
				revisions.Bump(id, kind.Value);

			if (e.Property == ItemsControl.ItemsSourceProperty)
				Follow(e.NewValue as INotifyCollectionChanged);
		}

		// A control's own properties are not the only way its data moves: a grid whose rows arrive
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

		public void ViewChanged() => revisions.Bump(id, UiRevisionKinds.View);

		private static UiRevisionKinds? KindOf(AvaloniaProperty property)
		{
			if (property == Visual.BoundsProperty)
				return UiRevisionKinds.Layout;

			if (property == Visual.IsVisibleProperty ||
				property == InputElement.IsEnabledProperty ||
				property == SelectingItemsControl.SelectedIndexProperty ||
				property == TreeViewItem.IsExpandedProperty ||
				property == Expander.IsExpandedProperty)
			{
				return UiRevisionKinds.View;
			}

			if (property == TextBox.TextProperty ||
				property == ToggleButton.IsCheckedProperty ||
				property == ContentControl.ContentProperty ||
				property == ItemsControl.ItemsSourceProperty ||
				property == ItemsControl.ItemCountProperty)
			{
				return UiRevisionKinds.State;
			}

			return null;
		}
	}
}
