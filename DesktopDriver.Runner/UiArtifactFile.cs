namespace StockSharp.DesktopDriver.Runner;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Client;
using StockSharp.DesktopDriver.Diagnostics;

/// <summary>
/// Brings an artifact the application holds across as a file.
/// </summary>
/// <remarks>
/// A picture crosses the pipe in pieces. Asking for the whole thing at once would be a message larger
/// than the protocol accepts, and the refusal would arrive as a failed read rather than as a size.
/// </remarks>
public static class UiArtifactFile
{
	// Well under the protocol's own message limit, so a piece plus its envelope always fits.
	private const int _pieceBytes = 256 * 1024;

	/// <summary>
	/// Writes one out.
	/// </summary>
	/// <param name="client">The connected client.</param>
	/// <param name="artifactId">Which artifact.</param>
	/// <param name="path">Where to write it.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>How many bytes it holds.</returns>
	public static async Task<long> SaveAsync(
		UiAutomationClient client,
		string artifactId,
		string path,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentException.ThrowIfNullOrEmpty(artifactId);
		ArgumentException.ThrowIfNullOrEmpty(path);

		var directory = Path.GetDirectoryName(Path.GetFullPath(path));

		if (!string.IsNullOrEmpty(directory))
			Directory.CreateDirectory(directory);

		await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);

		return await CopyAsync(client, artifactId, file, cancellationToken);
	}

	/// <summary>
	/// Reads one into memory.
	/// </summary>
	/// <param name="client">The connected client.</param>
	/// <param name="artifactId">Which artifact.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>Its bytes.</returns>
	public static async Task<byte[]> ReadAsync(
		UiAutomationClient client,
		string artifactId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentException.ThrowIfNullOrEmpty(artifactId);

		using var memory = new MemoryStream();

		await CopyAsync(client, artifactId, memory, cancellationToken);

		return memory.ToArray();
	}

	private static async Task<long> CopyAsync(
		UiAutomationClient client,
		string artifactId,
		Stream destination,
		CancellationToken cancellationToken)
	{
		var offset = 0L;

		while (true)
		{
			var piece = await client.ReadArtifactAsync(
				new UiArtifactReadRequest(artifactId, offset, _pieceBytes), cancellationToken);

			if (piece.Offset != offset)
			{
				throw new InvalidOperationException(
					$"The application answered from {piece.Offset} when {offset} was asked for.");
			}

			if (piece.Data is { Length: > 0 })
			{
				await destination.WriteAsync(piece.Data, cancellationToken);
				offset += piece.Data.Length;
			}

			if (piece.IsLast)
				return piece.TotalBytes;

			if (piece.Data is not { Length: > 0 })
			{
				// Neither more bytes nor an end: reading on would spin forever against a broken endpoint.
				throw new InvalidOperationException(
					$"The application sent nothing at {offset} and did not say the artifact had ended.");
			}
		}
	}
}
