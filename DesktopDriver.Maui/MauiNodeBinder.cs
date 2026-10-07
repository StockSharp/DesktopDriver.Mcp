namespace StockSharp.DesktopDriver.Maui;

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Maui;
using Microsoft.Maui.Controls;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// Turns the elements of a MAUI application into nodes: gives each one its address, registers it, and keeps
/// its revisions moving while anybody may be reading it.
/// </summary>
/// <param name="registry">Where nodes are registered.</param>
/// <param name="revisions">Where a change of a watched node is reported.</param>
public sealed class MauiNodeBinder(IUiNodeRegistry registry, IUiRevisionSink revisions) : IDisposable
{
	private readonly IUiNodeRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
	private readonly MauiRevisionWatcher _watcher = new(revisions);

	/// <inheritdoc />
	public void Dispose() => _watcher.Dispose();

	/// <summary>
	/// Rules a module adds to name the kind of a control of its own, so that the control is told apart from
	/// the elements it is drawn with.
	/// </summary>
	public IList<Func<Element, string>> KindRules { get; } = [];

	/// <summary>
	/// Registers an element as a node.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns>The node, or <see langword="null"/> for an element that is in no window.</returns>
	public UiNodeRef Bind(Element element)
	{
		var id = UiAutomationNames.GetNodeId(element);

		if (id is null)
			return null;

		// Bound and watched together: a node nobody watches has revisions that never move, and a guard or a
		// wait against it would be answered by an element that is quietly changing underneath.
		_watcher.Watch(element, id);

		return _registry.Register(new UiSubject(id, element), Kind(element), visualCreated: true);
	}

	private string Kind(Element element)
	{
		foreach (var rule in KindRules)
		{
			if (rule(element) is { Length: > 0 } kind)
				return kind;
		}

		return KindOf(element);
	}

	/// <summary>
	/// The elements a reader is shown under an element: the significant ones, with the layouts between them
	/// passed through.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <param name="includeVisualInternals">Every element rather than the significant ones.</param>
	/// <returns>The children.</returns>
	public IEnumerable<Element> GetSemanticChildren(Element element, bool includeVisualInternals)
	{
		ArgumentNullException.ThrowIfNull(element);

		foreach (var child in Children(element))
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

	/// <summary>
	/// What is directly inside an element.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns>The children.</returns>
	public static IEnumerable<Element> Children(Element element)
	{
		var children = element is IVisualTreeElement visual
			? visual.GetVisualChildren().OfType<Element>()
			: [];

		// A page shown over the window is drawn in it, and the window is where a reader looks for it.
		if (element is Window window && window.Navigation is { } navigation)
			children = children.Concat(navigation.ModalStack.Where(page => page is not null)).Distinct();

		return children;
	}

	/// <summary>
	/// Whether an element is shown to a reader rather than passed through.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns><see langword="true"/> when it is.</returns>
	public bool IsSignificant(Element element)
	{
		if (element is null)
			return false;

		foreach (var rule in KindRules)
		{
			if (rule(element) is { Length: > 0 })
				return true;
		}

		return IsStandard(element);
	}

	/// <summary>
	/// Whether an element is one of the standard controls a reader is always shown.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns><see langword="true"/> when it is.</returns>
	public static bool IsStandard(Element element)
	{
		if (element is null)
			return false;

		if (UiAutomationNames.HasStableId(element))
			return true;

		if (!string.IsNullOrEmpty(UiAutomationNames.GetScopeId(element)))
			return true;

		return element is Window or Page or BaseShellItem or ContentView or
			Button or ImageButton or InputView or Picker or DatePicker or TimePicker or
			CheckBox or Switch or RadioButton or Slider or Stepper or ProgressBar or ActivityIndicator or
			Label or ItemsView or WebView;
	}

	/// <summary>
	/// The kind a standard control is reported as.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <returns>The kind.</returns>
	public static string KindOf(Element element)
		=> element switch
		{
			null => "unknown",
			Window => "window",
			Shell => "shell",
			// What a person picks to change what the window shows: a flyout entry, a tab of a shell.
			BaseShellItem => "tabItem",
			TabbedPage => "tabs",
			Page => "page",
			CheckBox or Switch or RadioButton => "toggle",
			Button or ImageButton => "button",
			InputView => "textBox",
			Picker => "comboBox",
			ItemsView => "list",
			ContentView => "panel",
			_ => "control",
		};
}
