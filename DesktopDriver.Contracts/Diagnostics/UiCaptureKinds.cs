namespace StockSharp.DesktopDriver.Diagnostics;

/// <summary>
/// How a picture was taken.
/// </summary>
/// <remarks>
/// Redrawing a control into a bitmap and photographing the screen are different evidence. The first shows
/// what the control would draw, even if another window is over it; only the second shows what a person
/// would have seen.
/// </remarks>
public static class UiCaptureKinds
{
	/// <summary>The control drew itself into a bitmap.</summary>
	public const string ControlRender = "controlRender";

	/// <summary>The window's own contents were captured.</summary>
	public const string WindowCapture = "windowCapture";

	/// <summary>The screen was captured.</summary>
	public const string ScreenCapture = "screenCapture";
}
