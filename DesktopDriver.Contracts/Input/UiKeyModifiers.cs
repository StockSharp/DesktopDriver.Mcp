namespace StockSharp.DesktopDriver.Input;

using System;

/// <summary>
/// Which modifier keys are held down.
/// </summary>
[Flags]
public enum UiKeyModifiers
{
	/// <summary>None.</summary>
	None = 0,

	/// <summary>Shift.</summary>
	Shift = 1 << 0,

	/// <summary>Control.</summary>
	Control = 1 << 1,

	/// <summary>Alt.</summary>
	Alt = 1 << 2,

	/// <summary>The platform key: Windows or Command.</summary>
	Meta = 1 << 3,
}
