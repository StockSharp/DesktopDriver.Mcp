namespace StockSharp.DesktopDriver.Client;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// The client's half of the framing: four bytes of length, then that many bytes of UTF-8.
/// </summary>
/// <remarks>
/// Written out here rather than shared with the host so that a test, a command line or an agent tool
/// carries the client alone and never the endpoint. Nothing that merely talks to an application should
/// be able to open one.
/// </remarks>
internal static class UiPipeFramingBridge
{
	private const int _maxMessageBytes = 1024 * 1024;

	public static async Task WriteAsync(Stream stream, string json, CancellationToken cancellationToken)
	{
		var payload = Encoding.UTF8.GetBytes(json);

		if (payload.Length > _maxMessageBytes)
			throw new InvalidOperationException($"A message of {payload.Length} bytes is larger than the protocol allows.");

		var header = new byte[4];
		BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)payload.Length);

		await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
		await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
		await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
	}

	public static async Task<string> ReadAsync(Stream stream, CancellationToken cancellationToken)
	{
		var header = new byte[4];

		if (!await FillAsync(stream, header, cancellationToken).ConfigureAwait(false))
			return null;

		var length = BinaryPrimitives.ReadUInt32LittleEndian(header);

		if (length > _maxMessageBytes)
			throw new InvalidOperationException($"The host announced a message of {length} bytes.");

		var payload = new byte[length];

		if (!await FillAsync(stream, payload, cancellationToken).ConfigureAwait(false))
			throw new EndOfStreamException("The host stopped in the middle of a message.");

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
				return offset == 0 ? false : throw new EndOfStreamException("The host stopped in the middle of a message.");

			offset += read;
		}

		return true;
	}
}
