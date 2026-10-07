namespace StockSharp.DesktopDriver.Serialization;

using System;

/// <summary>
/// Turns a CLR member name into the name it travels under.
/// </summary>
internal static class UiWireNames
{
	/// <summary>
	/// Lower-cases the first letter, which is the whole of the protocol's naming rule.
	/// </summary>
	/// <param name="name">The CLR name.</param>
	/// <returns>The wire name.</returns>
	public static string ToWire(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		return char.IsLower(name[0]) ? name : char.ToLowerInvariant(name[0]) + name[1..];
	}
}
