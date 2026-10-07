namespace StockSharp.DesktopDriver.Tests.Wpf;

using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Wpf;

/// <summary>
/// A WPF window photographed the way the desktop has it.
/// </summary>
/// <remarks>
/// The title bar of a window whose frame the system draws is in no visual tree the window owns, so only a picture
/// asked of the desktop shows the window the way a person sees it - and the way the other backends picture theirs.
/// </remarks>
[TestClass]
public class WindowSurfaceCaptureTests : BaseTestClass
{
	private static readonly Color _ground = Color.FromRgb(0x12, 0x9A, 0x3B);

	[STATestMethod]
	public void AWindowIsPhotographedWithItsFrame()
	{
		var window = new Window
		{
			Width = 320,
			Height = 240,
			Left = 80,
			Top = 80,
			WindowStartupLocation = WindowStartupLocation.Manual,
			Background = new SolidColorBrush(_ground),
			ShowActivated = false,
			ShowInTaskbar = false,
			Topmost = true,
		};

		window.Show();

		try
		{
			var scaling = VisualTreeHelper.GetDpi(window).DpiScaleX;
			var picture = Photographed(window, scaling);

			IsTrue(Math.Abs(picture.PixelWidth - window.ActualWidth * scaling) <= 1, $"{picture.PixelWidth} pixels wide, the window is {window.ActualWidth * scaling}.");
			IsTrue(Math.Abs(picture.PixelHeight - window.ActualHeight * scaling) <= 1, $"{picture.PixelHeight} pixels tall, the window is {window.ActualHeight * scaling}.");

			AreEqual(_ground, PixelAt(picture, picture.PixelWidth / 2, picture.PixelHeight / 2));
			AreNotEqual(_ground, PixelAt(picture, picture.PixelWidth / 2, (int)(8 * scaling)), "The title bar is not in the picture.");
		}
		finally
		{
			window.Close();
		}
	}

	[STATestMethod]
	public void AWindowTheDesktopHasNotMadeIsRefused()
		=> Throws<Exception>(() => WindowSurfaceCapture.Take(new Window(), 96));

	// A window is on the desktop some frames after it was shown: until it has been drawn there, the desktop has a
	// blank rectangle to give.
	private static BitmapSource Photographed(Window window, double scaling)
	{
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

		while (true)
		{
			window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

			var picture = WindowSurfaceCapture.Take(window, 96 * scaling).Image;

			if (PixelAt(picture, picture.PixelWidth / 2, picture.PixelHeight / 2) == _ground || DateTime.UtcNow >= deadline)
				return picture;

			Thread.Sleep(100);
		}
	}

	private static Color PixelAt(BitmapSource picture, int x, int y)
	{
		var pixel = new byte[4];

		picture.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);

		return Color.FromRgb(pixel[2], pixel[1], pixel[0]);
	}
}
