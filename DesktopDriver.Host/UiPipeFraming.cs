namespace StockSharp.DesktopDriver.Host;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// How a message is put on the pipe and taken off it.
/// </summary>
/// <remarks>
/// Four bytes of length, then that many bytes of UTF-8. The length is checked against the limit before
/// anything is allocated: a peer that announces a gigabyte must not be able to make the host reserve it.
/// </remarks>
public static class UiPipeFraming
{
	/// <summary>
	/// The largest message the protocol carries.
	/// </summary>
	public const int MaxMessageBytes = 1024 * 1024;

	/// <summary>
	/// Writes one message.
	/// </summary>
	/// <param name="stream">The pipe.</param>
	/// <param name="json">The message.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>A task that completes when it is written.</returns>
	public static async Task WriteAsync(Stream stream, string json, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(stream);
		ArgumentNullException.ThrowIfNull(json);

		var payload = Encoding.UTF8.GetBytes(json);

		if (payload.Length > MaxMessageBytes)
			throw new InvalidOperationException($"A message of {payload.Length} bytes is larger than the protocol allows.");

		var header = new byte[4];
		BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)payload.Length);

		await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
		await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
		await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Reads one message.
	/// </summary>
	/// <param name="stream">The pipe.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The message, or <see langword="null"/> when the peer closed the connection.</returns>
	public static async Task<string> ReadAsync(Stream stream, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(stream);

		var header = new byte[4];

		if (!await FillAsync(stream, header, cancellationToken).ConfigureAwait(false))
			return null;

		var length = BinaryPrimitives.ReadUInt32LittleEndian(header);

		if (length > MaxMessageBytes)
			throw new InvalidOperationException($"The peer announced a message of {length} bytes.");

		var payload = new byte[length];

		if (!await FillAsync(stream, payload, cancellationToken).ConfigureAwait(false))
			throw new EndOfStreamException("The peer stopped in the middle of a message.");

		return Encoding.UTF8.GetString(payload);
	}

	private static async Task<bool> FillAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
	{
		var offset = 0;

		while (offset < buffer.Length)
		{
			var read = await stream
				.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken)
				.ConfigureAwait(false);

			if (read == 0)
				return offset == 0 ? false : throw new EndOfStreamException("The peer stopped in the middle of a message.");

			offset += read;
		}

		return true;
	}
}
