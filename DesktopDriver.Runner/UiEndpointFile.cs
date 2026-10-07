namespace StockSharp.DesktopDriver.Runner;

using System;
using System.IO;

using StockSharp.DesktopDriver.Serialization;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// The file a product writes once its endpoint is open.
/// </summary>
/// <remarks>
/// The product writes it beside its place and moves it in, so it is never read half-written. Whatever scans
/// new files on the machine can still hold it for a moment after it appears, and a read in that moment is
/// refused rather than wrong.
/// </remarks>
public static class UiEndpointFile
{
	/// <summary>
	/// Reads the endpoint, if the file can be read now.
	/// </summary>
	/// <param name="path">The file.</param>
	/// <param name="endpoint">What the product published, when the file was read.</param>
	/// <returns><see langword="true"/> when it was read; <see langword="false"/> when the file is not there yet or
	/// somebody else is holding it.</returns>
	public static bool TryRead(string path, out UiEndpointInfo endpoint)
	{
		ArgumentException.ThrowIfNullOrEmpty(path);

		endpoint = null;

		if (!File.Exists(path))
			return false;

		try
		{
			endpoint = UiJson.Read<UiEndpointInfo>(File.ReadAllText(path));
			return true;
		}
		catch (Exception error) when (error is IOException or UnauthorizedAccessException)
		{
			return false;
		}
	}
}
