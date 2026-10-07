namespace StockSharp.DesktopDriver.Avalonia.Docking;

using System;

using global::Avalonia.Controls;

using Dock.Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;

/// <summary>
/// Tells the module when a workspace has changed what it shows.
/// </summary>
/// <remarks>
/// A workspace draws a tree of groups and panels that the control itself knows nothing about: opening a
/// panel, closing one, moving one between groups or bringing a tab to the front all happen on the model,
/// and the control's own properties do not move. A watcher listening only to the control therefore sees
/// none of it, and a caller waiting for a panel it just opened waits until it gives up.
/// </remarks>
internal static class DockViewRevisions
{
	/// <summary>
	/// Follows the factory a workspace's layout is driven through.
	/// </summary>
	/// <param name="control">The control that was just bound.</param>
	/// <param name="changed">To be called when what the workspace shows changed.</param>
	public static void Watch(Control control, Action changed)
	{
		if (control is not DockControl workspace)
			return;

		ArgumentNullException.ThrowIfNull(changed);

		var follower = new Follower(workspace, changed);

		follower.Rebind();

		// The factory arrives with the layout, and an application that builds its workspace after the
		// control is in the tree has neither of them at the moment this runs.
		workspace.PropertyChanged += (_, _) => follower.Rebind();
	}

	private sealed class Follower(DockControl workspace, Action changed)
	{
		private IFactory _factory;

		public void Rebind()
		{
			// An application may hand the control a factory, or only a layout that already carries one.
			var factory = workspace.Factory ?? (workspace.Layout as IDockable)?.Factory;

			if (ReferenceEquals(_factory, factory))
				return;

			if (_factory is not null)
				Unsubscribe(_factory);

			_factory = factory;

			if (_factory is not null)
				Subscribe(_factory);
		}

		// Everything that changes which panels are drawn and in what order. Focus is deliberately not
		// here: a workspace that moved the keyboard somewhere shows exactly what it showed before.
		private void Subscribe(IFactory factory)
		{
			factory.DockableAdded += Moved;
			factory.DockableRemoved += Moved;
			factory.DockableClosed += Moved;
			factory.DockableDocked += Moved;
			factory.DockableUndocked += Moved;
			factory.DockableMoved += Moved;
			factory.DockableSwapped += Moved;
			factory.DockableHidden += Moved;
			factory.DockableRestored += Moved;
			factory.DockablePinned += Moved;
			factory.DockableUnpinned += Moved;
			factory.ActiveDockableChanged += Moved;
			factory.WindowAdded += Moved;
			factory.WindowClosed += Moved;
		}

		private void Unsubscribe(IFactory factory)
		{
			factory.DockableAdded -= Moved;
			factory.DockableRemoved -= Moved;
			factory.DockableClosed -= Moved;
			factory.DockableDocked -= Moved;
			factory.DockableUndocked -= Moved;
			factory.DockableMoved -= Moved;
			factory.DockableSwapped -= Moved;
			factory.DockableHidden -= Moved;
			factory.DockableRestored -= Moved;
			factory.DockablePinned -= Moved;
			factory.DockableUnpinned -= Moved;
			factory.ActiveDockableChanged -= Moved;
			factory.WindowAdded -= Moved;
			factory.WindowClosed -= Moved;
		}

		// Each of them carries its own arguments and none of them is read: what the workspace shows has
		// changed, and the reader is going to ask the workspace what it shows now.
		private void Moved<T>(object sender, T e) where T : EventArgs => changed();
	}
}
