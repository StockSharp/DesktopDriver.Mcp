namespace StockSharp.DesktopDriver.Avalonia.Grids;

using System;
using System.Collections.Specialized;

using global::Avalonia.Collections;
using global::Avalonia.Controls;

/// <summary>
/// Tells the module when a table has changed what it shows.
/// </summary>
/// <remarks>
/// A table decides what it shows, and in what order, on the collection view it builds for its source -
/// not on any property of its own. Nothing about sorting or grouping a table therefore reaches a watcher
/// that only listens to the control, and a caller waiting for a table to finish re-ordering waits until
/// it gives up on a table that re-ordered at once.
/// </remarks>
internal static class GridViewRevisions
{
	/// <summary>
	/// Follows a table's collection view, and whatever replaces it.
	/// </summary>
	/// <param name="control">The control that was just bound.</param>
	/// <param name="changed">To be called when what the table shows changed.</param>
	public static void Watch(Control control, Action changed)
	{
		if (control is not DataGrid grid)
			return;

		ArgumentNullException.ThrowIfNull(changed);

		var follower = new Follower(grid, changed);

		follower.Rebind();

		// The view is rebuilt whenever the table is given a different source, and the old one's
		// descriptions then belong to nothing.
		grid.PropertyChanged += (_, e) =>
		{
			if (e.Property != DataGrid.CollectionViewProperty)
				return;

			follower.Rebind();
			changed();
		};
	}

	private sealed class Follower(DataGrid grid, Action changed)
	{
		private INotifyCollectionChanged _sorts;
		private INotifyCollectionChanged _groups;

		public void Rebind()
		{
			var view = grid.CollectionView;

			_sorts = Follow(_sorts, view?.SortDescriptions as INotifyCollectionChanged);
			_groups = Follow(_groups, (view as DataGridCollectionView)?.GroupDescriptions);
		}

		private INotifyCollectionChanged Follow(INotifyCollectionChanged current, INotifyCollectionChanged next)
		{
			if (ReferenceEquals(current, next))
				return current;

			if (current is not null)
				current.CollectionChanged -= OnChanged;

			if (next is not null)
				next.CollectionChanged += OnChanged;

			return next;
		}

		// A second click on the same header turns the sort round without adding or removing a description,
		// so the collection reports a replacement rather than a change of length; either way the table is
		// showing its rows in a different order than it was.
		private void OnChanged(object sender, NotifyCollectionChangedEventArgs e) => changed();
	}
}
