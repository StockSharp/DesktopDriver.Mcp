namespace StockSharp.DesktopDriver.Wpf;

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// Photographs a window the way the desktop has it, frame and title bar included.
/// </summary>
/// <remarks>
/// What a window draws into a bitmap is its client area. A window whose frame the system draws has its title bar
/// in no visual tree the window owns, so a redraw of it leaves the bar out, and the two heads of a product - one
/// redrawn, one photographed - would not be the same kind of picture. A picture that has to show the product as
/// a person sees it is asked of the desktop.
/// </remarks>
internal static class WindowSurfaceCapture
{
	// Everything the window has, composed layers included; without it a window drawn by the graphics card comes
	// back blank.
	private const uint _renderFullContent = 2;

	private const int _bitCount = 32;
	private const uint _rgb = 0;
	private const uint _dibRgbColors = 0;

	[StructLayout(LayoutKind.Sequential)]
	private struct Rectangle
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BitmapInfoHeader
	{
		public uint Size;
		public int Width;
		public int Height;
		public ushort Planes;
		public ushort BitCount;
		public uint Compression;
		public uint SizeImage;
		public int XPelsPerMeter;
		public int YPelsPerMeter;
		public uint ColorUsed;
		public uint ColorImportant;
	}

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetWindowRect(IntPtr window, out Rectangle rectangle);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool PrintWindow(IntPtr window, IntPtr deviceContext, uint flags);

	[DllImport("user32.dll")]
	private static extern IntPtr GetDC(IntPtr window);

	[DllImport("user32.dll")]
	private static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreateDIBSection(IntPtr deviceContext, ref BitmapInfoHeader header, uint usage, out IntPtr bits, IntPtr section, uint offset);

	[DllImport("gdi32.dll")]
	private static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr handle);

	[DllImport("gdi32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool DeleteObject(IntPtr handle);

	[DllImport("gdi32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool DeleteDC(IntPtr deviceContext);

	/// <summary>
	/// A window as the desktop has it, and where the desktop has it.
	/// </summary>
	/// <param name="Image">The picture, in screen pixels.</param>
	/// <param name="Origin">The top left of the window on the screen, in screen pixels.</param>
	public readonly record struct WindowPicture(BitmapSource Image, Point Origin);

	/// <summary>
	/// Photographs a window.
	/// </summary>
	/// <param name="window">The window.</param>
	/// <param name="dpi">The resolution to stamp the picture with.</param>
	/// <returns>The picture and where it was taken from.</returns>
	public static WindowPicture Take(Window window, double dpi)
	{
		ArgumentNullException.ThrowIfNull(window);

		var handle = new WindowInteropHelper(window).Handle;

		if (handle == IntPtr.Zero)
			throw UiErrors.Fail(UiErrorCodes.NotCreated, "That window has not been created by the desktop yet.");

		if (!GetWindowRect(handle, out var rectangle))
			throw UiErrors.Fail(UiErrorCodes.NotCreated, "The desktop would not say where that window is.");

		var width = rectangle.Right - rectangle.Left;
		var height = rectangle.Bottom - rectangle.Top;

		if (width <= 0 || height <= 0)
			throw UiErrors.Fail(UiErrorCodes.NotInteractable, "That window has no size to photograph.");

		var header = new BitmapInfoHeader
		{
			Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
			Width = width,
			// Negative, so that the first row of the picture is the top of the window rather than its bottom -
			// which is the order everything downstream reads.
			Height = -height,
			Planes = 1,
			BitCount = _bitCount,
			Compression = _rgb,
		};

		var screen = GetDC(IntPtr.Zero);
		var memory = IntPtr.Zero;
		var section = IntPtr.Zero;

		try
		{
			memory = CreateCompatibleDC(screen);
			section = CreateDIBSection(screen, ref header, _dibRgbColors, out var pixels, IntPtr.Zero, 0);

			if (section == IntPtr.Zero || pixels == IntPtr.Zero)
				throw UiErrors.Fail(UiErrorCodes.NotCreated, "The desktop would not give a surface to draw that window on.");

			var previous = SelectObject(memory, section);

			try
			{
				if (!PrintWindow(handle, memory, _renderFullContent))
					throw UiErrors.Fail(UiErrorCodes.NotCreated, "The desktop refused to draw that window.");

				var stride = width * (_bitCount / 8);
				var buffer = new byte[(long)stride * height];

				Marshal.Copy(pixels, buffer, 0, buffer.Length);

				// What PrintWindow leaves behind is opaque, but it does not say so: the alpha byte of every pixel
				// it never touched stays zero, and a picture saved from that is a window with holes in it.
				for (var index = 3; index < buffer.Length; index += 4)
					buffer[index] = 0xFF;

				var image = BitmapSource.Create(width, height, dpi, dpi, PixelFormats.Bgra32, null, buffer, stride);

				image.Freeze();

				return new(image, new Point(rectangle.Left, rectangle.Top));
			}
			finally
			{
				SelectObject(memory, previous);
			}
		}
		finally
		{
			if (section != IntPtr.Zero)
				DeleteObject(section);

			if (memory != IntPtr.Zero)
				DeleteDC(memory);

			ReleaseDC(IntPtr.Zero, screen);
		}
	}
}
