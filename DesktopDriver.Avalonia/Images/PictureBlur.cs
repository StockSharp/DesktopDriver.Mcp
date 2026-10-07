namespace StockSharp.DesktopDriver.Avalonia;

using System;

using global::Avalonia;

/// <summary>
/// Blurs part of a picture until nothing written in it can be read back.
/// </summary>
/// <remarks>
/// A Gaussian, which weakens detail the more the finer it is, spread over half the height of a line of
/// text: what tells one letter from the next then falls below what eight bits of colour can hold, and what
/// is left is where the text was, not what it said. A tall area - the values of a table column - is spread
/// as one line would be, so its rows stay rows.
/// </remarks>
internal static class PictureBlur
{
	// In the window's units.
	private const double _lineHeight = 20;
	private const double _leastSpread = 4;

	/// <summary>
	/// Blurs one area of a picture in place.
	/// </summary>
	/// <param name="pixels">The picture, four bytes a pixel, rows one after another.</param>
	/// <param name="width">The picture's width in pixels.</param>
	/// <param name="height">The picture's height in pixels.</param>
	/// <param name="area">What to blur, in the window's units.</param>
	/// <param name="scaling">Pixels per unit.</param>
	/// <remarks>
	/// Only pixels inside the area are read, so nothing next to it is carried into it, and only pixels
	/// inside it are written.
	/// </remarks>
	public static void Apply(byte[] pixels, int width, int height, Rect area, double scaling)
	{
		ArgumentNullException.ThrowIfNull(pixels);

		var left = Math.Max(0, (int)Math.Floor(area.X * scaling));
		var top = Math.Max(0, (int)Math.Floor(area.Y * scaling));
		var right = Math.Min(width, (int)Math.Ceiling(area.Right * scaling));
		var bottom = Math.Min(height, (int)Math.Ceiling(area.Bottom * scaling));

		if (right - left < 1 || bottom - top < 1)
			return;

		var spread = Math.Max(_leastSpread, Math.Min(area.Height, _lineHeight) / 2) * scaling;
		var kernel = Kernel(spread);

		var across = right - left;
		var down = bottom - top;
		var source = new float[across * down * 4];
		var target = new float[source.Length];

		for (var y = 0; y < down; y++)
		{
			for (var x = 0; x < across; x++)
			{
				var from = ((top + y) * width + left + x) * 4;
				var to = (y * across + x) * 4;

				for (var channel = 0; channel < 4; channel++)
					source[to + channel] = pixels[from + channel];
			}
		}

		Pass(source, target, across, down, kernel, 4, across * 4);
		Pass(target, source, down, across, kernel, across * 4, 4);

		for (var y = 0; y < down; y++)
		{
			for (var x = 0; x < across; x++)
			{
				var from = (y * across + x) * 4;
				var to = ((top + y) * width + left + x) * 4;

				for (var channel = 0; channel < 4; channel++)
					pixels[to + channel] = (byte)Math.Clamp(Math.Round(source[from + channel]), 0, 255);
			}
		}
	}

	private static float[] Kernel(double spread)
	{
		var reach = (int)Math.Ceiling(spread * 3);
		var kernel = new float[reach * 2 + 1];
		var total = 0.0;

		for (var offset = -reach; offset <= reach; offset++)
		{
			var weight = Math.Exp(-(offset * offset) / (2 * spread * spread));

			kernel[offset + reach] = (float)weight;
			total += weight;
		}

		for (var index = 0; index < kernel.Length; index++)
			kernel[index] = (float)(kernel[index] / total);

		return kernel;
	}

	// One direction of the blur: along each of 'lines' lines of 'length' pixels, 'step' apart within a line
	// and 'gap' apart from one line to the next. Past either end the edge pixel stands in for what is
	// outside, which is how nothing beyond the area is read.
	private static void Pass(float[] from, float[] to, int length, int lines, float[] kernel, int step, int gap)
	{
		var reach = kernel.Length / 2;

		for (var line = 0; line < lines; line++)
		{
			var start = line * gap;

			for (var position = 0; position < length; position++)
			{
				for (var channel = 0; channel < 4; channel++)
				{
					var sum = 0f;

					for (var offset = -reach; offset <= reach; offset++)
					{
						var at = Math.Clamp(position + offset, 0, length - 1);

						sum += kernel[offset + reach] * from[start + at * step + channel];
					}

					to[start + position * step + channel] = sum;
				}
			}
		}
	}
}
