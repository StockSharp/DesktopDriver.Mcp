namespace StockSharp.DesktopDriver.Runtime;

using System;
using System.Collections.Generic;
using System.Threading;

using StockSharp.DesktopDriver.Diagnostics;

/// <summary>
/// Holds the images and files a session produced, and hands them out in pieces.
/// </summary>
/// <remarks>
/// Identifiers are opaque and mean something only inside the session that issued them. They are not
/// paths, and no request can turn one into a path: the store hands back what it was given and nothing
/// else on the disk.
/// <para>
/// Bounded in total size. An image is large, a long run produces many, and a store that kept them all
/// would end the run by exhausting memory rather than by failing a test.
/// </para>
/// </remarks>
public sealed class UiArtifactStore(long maximumBytes = 64 * 1024 * 1024)
{
	private sealed class Entry
	{
		public byte[] Data;
		public string MimeType;
		public DateTime CreatedAt;
	}

	private readonly Lock _sync = new();
	private readonly Dictionary<string, Entry> _entries = [];
	private readonly Queue<string> _order = new();
	private readonly long _maximumBytes = maximumBytes;

	private long _held;

	/// <summary>
	/// How many bytes are held.
	/// </summary>
	public long HeldBytes
	{
		get
		{
			using (_sync.EnterScope())
				return _held;
		}
	}

	/// <summary>
	/// Keeps something and returns the identifier it will be asked for by.
	/// </summary>
	/// <param name="data">The bytes.</param>
	/// <param name="mimeType">What kind of thing it is.</param>
	/// <returns>The identifier.</returns>
	public string Add(byte[] data, string mimeType)
	{
		ArgumentNullException.ThrowIfNull(data);
		ArgumentException.ThrowIfNullOrEmpty(mimeType);

		var id = Guid.NewGuid().ToString("N");

		using (_sync.EnterScope())
		{
			_entries.Add(id, new Entry { Data = data, MimeType = mimeType, CreatedAt = DateTime.UtcNow });
			_order.Enqueue(id);
			_held += data.Length;

			while (_held > _maximumBytes && _order.Count > 1)
			{
				var oldest = _order.Dequeue();

				if (_entries.Remove(oldest, out var dropped))
					_held -= dropped.Data.Length;
			}
		}

		return id;
	}

	/// <summary>
	/// Reads part of something.
	/// </summary>
	/// <param name="request">Which part.</param>
	/// <returns>The bytes.</returns>
	public UiArtifactChunk Read(UiArtifactReadRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);

		using (_sync.EnterScope())
		{
			if (!_entries.TryGetValue(request.ArtifactId, out var entry))
			{
				throw UiErrors.Fail(
					UiErrorCodes.ArtifactNotFound,
					$"No artifact of this session is called '{request.ArtifactId}'.");
			}

			if (request.Offset < 0 || request.Offset > entry.Data.Length)
				throw UiErrors.Invalid("That offset is outside the artifact.");

			var maximum = request.MaxBytes <= 0 ? 64 * 1024 : Math.Min(request.MaxBytes, 64 * 1024);
			var length = (int)Math.Min(maximum, entry.Data.Length - request.Offset);
			var chunk = new byte[length];

			Array.Copy(entry.Data, request.Offset, chunk, 0, length);

			return new UiArtifactChunk(
				request.ArtifactId,
				request.Offset,
				chunk,
				request.Offset + length >= entry.Data.Length,
				entry.Data.Length);
		}
	}

	/// <summary>
	/// What kind of thing an artifact is.
	/// </summary>
	/// <param name="artifactId">The identifier.</param>
	/// <returns>Its media type.</returns>
	public string GetMimeType(string artifactId)
	{
		using (_sync.EnterScope())
		{
			return _entries.TryGetValue(artifactId, out var entry)
				? entry.MimeType
				: throw UiErrors.Fail(UiErrorCodes.ArtifactNotFound, $"No artifact called '{artifactId}'.");
		}
	}
}
