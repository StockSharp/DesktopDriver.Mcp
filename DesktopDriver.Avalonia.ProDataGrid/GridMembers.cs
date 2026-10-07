namespace StockSharp.DesktopDriver.Avalonia.Grids;

using System;
using System.Collections.Concurrent;
using System.Reflection;

/// <summary>
/// Reads a named member off a record a grid is showing.
/// </summary>
/// <remarks>
/// The path is the one the column itself sorts by, so what is read is what that column is about. The
/// value comes from the record rather than from the text on screen, and the reply says so: a number
/// compared against its formatted text passes or fails depending on the machine's locale.
/// </remarks>
internal static class GridMembers
{
	private static readonly ConcurrentDictionary<(Type Type, string Path), PropertyInfo[]> _paths = new();

	/// <summary>
	/// Reads a path such as <c>Security.Code</c> off a record.
	/// </summary>
	/// <param name="item">The record.</param>
	/// <param name="path">The path.</param>
	/// <param name="value">What was there.</param>
	/// <returns><see langword="true"/> when the whole path could be followed.</returns>
	public static bool TryRead(object item, string path, out object value)
	{
		value = null;

		if (item is null || string.IsNullOrEmpty(path))
			return false;

		var steps = _paths.GetOrAdd((item.GetType(), path), key => Resolve(key.Type, key.Path));

		if (steps is null)
			return false;

		var current = item;

		foreach (var step in steps)
		{
			if (current is null)
				return false;

			// The path was resolved against the type of the first record. A different type further along
			// means this record is not shaped the way the column expects, which is worth saying rather
			// than guessing at.
			if (!step.DeclaringType.IsInstanceOfType(current))
				return false;

			current = step.GetValue(current);
		}

		value = current;

		return true;
	}

	private static PropertyInfo[] Resolve(Type type, string path)
	{
		var steps = new PropertyInfo[path.Split('.').Length];
		var current = type;
		var index = 0;

		foreach (var name in path.Split('.'))
		{
			var property = current?.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);

			if (property is null)
				return null;

			steps[index++] = property;
			current = property.PropertyType;
		}

		return steps;
	}
}
