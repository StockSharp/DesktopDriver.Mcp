namespace StockSharp.DesktopDriver.Wpf;

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// Turns controls into registered nodes, and decides which of them belong in the tree a person would draw.
/// </summary>
/// <remarks>
/// A visual tree contains every border and presenter a template happens to use. A reply built from that
/// is unreadable and mostly template detail, so the default walk keeps the controls that mean something -
/// the ones the interface named, and the ones a person interacts with - and descends through the rest
/// without mentioning them. The full visual tree stays available for diagnosis, behind its own flag.
/// </remarks>
public sealed class WpfNodeBinder(IUiNodeRegistry registry, IUiRevisionSink revisions) : IDisposable
{
	private readonly IUiNodeRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
	private readonly WpfRevisionWatcher _watcher = new(revisions);

	/// <summary>
	/// Stops watching every control this has bound.
	/// </summary>
	/// <remarks>
	/// Watching a control means holding a handler on it, and a handler holds the control. An application
	/// that stops offering to be driven has to let go of the interface it was driving, or every window it
	/// ever opened stays in memory for as long as it runs.
	/// </remarks>
	public void Dispose() => _watcher.Dispose();

	/// <summary>
	/// What a library of its own calls its controls.
	/// </summary>
	/// <remarks>
	/// The kinds below are the ones WPF itself defines. A library that adds a control of a different
	/// kind - a table, a chart, a workspace - says so here, because this assembly cannot name a type it
	/// does not reference and a table reported as a control is a table nothing will think to read as one.
	/// The first rule that recognises a control decides.
	/// </remarks>
	public IList<Func<FrameworkElement, string>> KindRules { get; } = [];

	/// <summary>
	/// Registers a control and returns what callers will address it by.
	/// </summary>
	/// <param name="control">The control.</param>
	/// <returns>The reference, or <see langword="null"/> when the control has no address yet.</returns>
	public UiNodeRef Bind(FrameworkElement control)
	{
		var id = UiAutomationNames.GetNodeId(control);

		if (id is null)
			return null;

		// Bound and watched together: a node nobody watches has revisions that never move, and a guard or
		// a wait against it would be answered by a control that is quietly changing underneath.
		_watcher.Watch(control, id);

		return _registry.Register(new UiSubject(id, control), Kind(control), visualCreated: true);
	}

	private string Kind(FrameworkElement control)
	{
		foreach (var rule in KindRules)
		{
			if (rule(control) is { Length: > 0 } kind)
				return kind;
		}

		return KindOf(control);
	}

	/// <summary>
	/// The children of a control as the default tree shows them.
	/// </summary>
	/// <param name="control">The control.</param>
	/// <param name="includeVisualInternals">Whether to return the visual tree as it really is.</param>
	/// <returns>The children.</returns>
	public IEnumerable<FrameworkElement> GetSemanticChildren(FrameworkElement control, bool includeVisualInternals)
	{
		ArgumentNullException.ThrowIfNull(control);

		foreach (var child in VisualChildren(control))
		{
			if (includeVisualInternals || IsSignificant(child))
			{
				yield return child;
				continue;
			}

			foreach (var descendant in GetSemanticChildren(child, false))
				yield return descendant;
		}
	}

	// The visual tree answers with visuals rather than with elements. A child that is not an element has
	// neither a name nor a state to read, so it is left out along with everything below it.
	private static IEnumerable<FrameworkElement> VisualChildren(FrameworkElement control)
	{
		var count = VisualTreeHelper.GetChildrenCount(control);

		for (var i = 0; i < count; i++)
		{
			if (VisualTreeHelper.GetChild(control, i) is FrameworkElement child)
				yield return child;
		}
	}

	/// <summary>
	/// Whether a control is one the default tree mentions.
	/// </summary>
	/// <param name="control">The control.</param>
	/// <returns><see langword="true"/> when it is.</returns>
	/// <remarks>
	/// A library that gave a control a kind of its own through <see cref="KindRules"/> has said it is one
	/// of the things a caller looks for, so the tree names it. Without that a chart is an ordinary control
	/// of a type this assembly cannot reference, and the walk goes straight through it into its own
	/// drawing internals - which is to say the chart is not in the tree at all, and a caller asking what a
	/// panel is showing is told nothing rather than told about the chart.
	/// </remarks>
	public bool IsSignificant(FrameworkElement control)
	{
		if (control is null)
			return false;

		foreach (var rule in KindRules)
		{
			if (rule(control) is { Length: > 0 })
				return true;
		}

		return IsStandard(control);
	}

	/// <summary>
	/// Whether a control is one of the kinds WPF itself defines that the default tree mentions.
	/// </summary>
	/// <param name="control">The control.</param>
	/// <returns><see langword="true"/> when it is.</returns>
	public static bool IsStandard(FrameworkElement control)
	{
		if (control is null)
			return false;

		if (UiAutomationNames.HasStableId(control))
			return true;

		if (!string.IsNullOrEmpty(UiAutomationNames.GetScopeId(control)))
			return true;

		return control is ButtonBase or TextBox or Selector or ItemsControl or
			TextBlock or Window or UserControl or Slider or ProgressBar;
	}

	/// <summary>
	/// The registered node kind of a control.
	/// </summary>
	/// <param name="control">The control.</param>
	/// <returns>The kind.</returns>
	public static string KindOf(FrameworkElement control)
		=> control switch
		{
			null => "unknown",
			Window => "window",
			// Before ButtonBase: a toggle button is one, and the more specific kind is the useful one.
			ToggleButton => "toggle",
			ButtonBase => "button",
			TextBox => "textBox",
			TabControl => "tabs",
			// A tab is what a person clicks to change what the window shows, so it is worth telling apart
			// from the content control it happens to be.
			TabItem => "tabItem",
			ComboBox => "comboBox",
			// Before ItemsControl: a tree is one, and the more specific kind is the useful one.
			TreeView => "tree",
			Selector => "list",
			ItemsControl => "items",
			UserControl => "panel",
			_ => "control",
		};
}
