namespace StockSharp.DesktopDriver.Queries;

using System;
using System.Buffers.Binary;
using System.Text;

/// <summary>
/// Where a cut-short page left off, and what it was a page of.
/// </summary>
/// <remarks>
/// A page that was cut short is useless without one: the caller has read part of something and has no
/// way to ask for the rest. Asking again with a larger limit is not the same - the thing being read may
/// have moved in between, and the second answer would overlap the first without saying so.
/// <para>
/// The cursor carries what the page was of, not only where it stopped. Continuing a page of sorted rows
/// with a cursor from an unsorted one would hand back rows from two different orders as though they were
/// one list, and nothing in the answer would show it. So the operation and the request that produced the
/// page are both pinned, and a cursor from anything else is refused rather than honoured.
/// </para>
/// <para>
/// It is opaque on purpose. A caller that took it apart would be relying on a shape nobody promised, and
/// the first change to it would break them quietly.
/// </para>
/// </remarks>
public static class UiPageCursor
{
	// Three fields, fixed width: what it continues, what it was a page of, and where it stopped.
	private const int _size = sizeof(int) + sizeof(int) + sizeof(long);

	/// <summary>
	/// Makes a cursor for the next page.
	/// </summary>
	/// <param name="operation">The operation this continues, as named in the protocol.</param>
	/// <param name="request">Everything about the request that decides what the page holds.</param>
	/// <param name="nextIndex">The position the next page starts at.</param>
	/// <returns>The cursor.</returns>
	public static string Create(string operation, string request, long nextIndex)
	{
		ArgumentException.ThrowIfNullOrEmpty(operation);

		Span<byte> bytes = stackalloc byte[_size];

		BinaryPrimitives.WriteInt32LittleEndian(bytes, Fingerprint(operation));
		BinaryPrimitives.WriteInt32LittleEndian(bytes[sizeof(int)..], Fingerprint(request));
		BinaryPrimitives.WriteInt64LittleEndian(bytes[(sizeof(int) * 2)..], nextIndex);

		return Convert.ToBase64String(bytes);
	}

	/// <summary>
	/// Where to start reading, given what the caller asked for.
	/// </summary>
	/// <param name="cursor">The cursor the caller sent back, or <see langword="null"/>.</param>
	/// <param name="operation">The operation being served.</param>
	/// <param name="request">Everything about the request that decides what the page holds.</param>
	/// <param name="fallback">Where to start when there is no cursor.</param>
	/// <returns>The position to start at.</returns>
	/// <exception cref="ArgumentException">The cursor is not one of ours, or is not for this request.</exception>
	public static long StartAt(string cursor, string operation, string request, long fallback)
	{
		if (string.IsNullOrEmpty(cursor))
			return fallback;

		Span<byte> bytes = stackalloc byte[_size];

		if (!Convert.TryFromBase64String(cursor, bytes, out var written) || written != _size)
			throw new ArgumentException($"'{cursor}' is not a cursor this protocol issued.", nameof(cursor));

		if (BinaryPrimitives.ReadInt32LittleEndian(bytes) != Fingerprint(operation))
			throw new ArgumentException($"That cursor continues another operation, not {operation}.", nameof(cursor));

		if (BinaryPrimitives.ReadInt32LittleEndian(bytes[sizeof(int)..]) != Fingerprint(request))
		{
			throw new ArgumentException(
				"That cursor was issued for a different request. Continuing with it would join two " +
				"different lists into one answer.",
				nameof(cursor));
		}

		return BinaryPrimitives.ReadInt64LittleEndian(bytes[(sizeof(int) * 2)..]);
	}

	// Stable across processes and runs, unlike string.GetHashCode, which is randomised per process - a
	// cursor has to survive a caller that reconnects.
	private static int Fingerprint(string value)
	{
		if (string.IsNullOrEmpty(value))
			return 0;

		unchecked
		{
			var hash = 2166136261;

			foreach (var b in Encoding.UTF8.GetBytes(value))
			{
				hash ^= b;
				hash *= 16777619;
			}

			return (int)hash;
		}
	}
}
