namespace StockSharp.DesktopDriver.Avalonia;

using System.Collections.Generic;
using System.Linq;

using global::Avalonia.Controls;
using global::Avalonia.LogicalTree;

/// <summary>
/// Walks a tree the way a person reads it.
/// </summary>
/// <remarks>
/// Only the items the tree has actually built are walked. A tree builds what is inside an item when the
/// item is opened, so walking further would be opening it, and a closed item is usually what a test is
/// about.
/// </remarks>
internal static class AvaloniaTreeItems
{
	public readonly record struct Entry(TreeViewItem Item, string Key, string ParentKey, int Depth);

	public static IEnumerable<Entry> Shown(TreeView tree)
		=> Walk(Containers(tree), null, 0);

	public static string TextOf(TreeViewItem item)
	{
		var header = item.Header;

		if (header is string text)
			return text;

		if (header is null)
			return null;

		// A header that is a control shows whatever text it holds; the first piece of it is what a person
		// reads the row as.
		if (header is Control control)
		{
			var block = control as TextBlock ?? control.GetLogicalDescendants().OfType<TextBlock>().FirstOrDefault();

			return block?.Text;
		}

		var written = header.ToString();

		// An object whose text is just its type name has not named itself, and saying it has would give a
		// test a key that every object of that type shares.
		return written == header.GetType().FullName ? null : written;
	}

	private static IEnumerable<TreeViewItem> Containers(ItemsControl owner)
		=> owner.GetRealizedContainers().OfType<TreeViewItem>();

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
}
