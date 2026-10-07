namespace StockSharp.DesktopDriver.Wpf;

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

/// <summary>
/// Walks a tree the way a person reads it.
/// </summary>
/// <remarks>
/// Only the items the tree has actually built are walked. A tree builds what is inside an item when the
/// item is opened, so walking further would be opening it, and a closed item is usually what a test is
/// about.
/// </remarks>
internal static class WpfTreeItems
{
	/// <summary>
	/// One item, as the walk reads it.
	/// </summary>
	/// <param name="Item">The item itself.</param>
	/// <param name="Key">The item's path through the tree, which is what a test names it by.</param>
	/// <param name="ParentKey">The item it sits inside, or <see langword="null"/> at the top level.</param>
	/// <param name="Depth">How deep it is.</param>
	public readonly record struct Entry(TreeViewItem Item, string Key, string ParentKey, int Depth);

	/// <summary>
	/// Walks the items the tree is showing, in the order it shows them.
	/// </summary>
	/// <param name="tree">The tree to walk.</param>
	/// <returns>The items, each with its path, the item it sits inside and its depth.</returns>
	public static IEnumerable<Entry> Shown(TreeView tree)
		=> Walk(Containers(tree), null, 0);

	/// <summary>
	/// Reads what an item says.
	/// </summary>
	/// <param name="item">The item to read.</param>
	/// <returns>What it reads as, or <see langword="null"/> when it shows something that is not text.</returns>
	public static string TextOf(TreeViewItem item)
	{
		var header = item.Header;

		if (header is string text)
			return text;

		if (header is null)
			return null;

		// A header that is an element shows whatever text it holds; the first piece of it is what a person
		// reads the row as.
		if (header is FrameworkElement element)
		{
			var block = element as TextBlock
				?? LogicalDescendants(element).OfType<TextBlock>().FirstOrDefault()
				// Text a template made for the header is not in the logical tree, only in the visual one.
				?? VisualDescendants(element).OfType<TextBlock>().FirstOrDefault();

			return block?.Text;
		}

		var written = header.ToString();

		// An object whose text is just its type name has not named itself, and saying it has would give a
		// test a key that every object of that type shares.
		return written == header.GetType().FullName ? null : written;
	}

	private static IEnumerable<TreeViewItem> Containers(ItemsControl owner)
	{
		var generator = owner.ItemContainerGenerator;

		for (var index = 0; index < owner.Items.Count; index++)
		{
			// An item the tree has not built a container for yet comes back as null, and building it is
			// the tree's business, not the reader's.
			if (generator.ContainerFromIndex(index) is TreeViewItem item)
				yield return item;
		}
	}

	private static IEnumerable<Entry> Walk(IEnumerable<TreeViewItem> items, string parentKey, int depth)
	{
		// Two siblings that read the same get a number, so that naming one of them still names one of them.
		var seen = new Dictionary<string, int>();

		foreach (var item in items)
		{
			var text = TextOf(item) ?? $"item{seen.Count}";

			seen.TryGetValue(text, out var repeated);
			seen[text] = repeated + 1;

			var name = repeated == 0 ? text : $"{text}#{repeated + 1}";
			var key = parentKey is null ? name : $"{parentKey}/{name}";

			yield return new Entry(item, key, parentKey, depth);

			if (!item.IsExpanded)
				continue;

			foreach (var nested in Walk(Containers(item), key, depth + 1))
				yield return nested;
		}
	}

	private static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject root)
	{
		foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
		{
			yield return child;

			foreach (var nested in LogicalDescendants(child))
				yield return nested;
		}
	}

	private static IEnumerable<DependencyObject> VisualDescendants(Visual root)
	{
		var count = VisualTreeHelper.GetChildrenCount(root);

		for (var index = 0; index < count; index++)
		{
			var child = VisualTreeHelper.GetChild(root, index);

			yield return child;

			// Only a visual has visual children; asking anything else for them throws.
			if (child is Visual visual)
			{
				foreach (var nested in VisualDescendants(visual))
					yield return nested;
			}
		}
	}
}
