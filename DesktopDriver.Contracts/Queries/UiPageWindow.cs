namespace StockSharp.DesktopDriver.Queries;

using System;

/// <summary>
/// The bookkeeping every paged read does: what to skip, what to take, and what to say about the rest.
/// </summary>
/// <remarks>
/// One implementation, because every adapter was doing it by hand and doing it slightly differently -
/// and the differences were invisible until a caller tried to read the second page of something and
/// found there was no way to ask for it.
/// <para>
/// The total counts everything that matched, not everything returned. A caller that sees ten of two
/// hundred knows there are two hundred; a total that echoed the page size would be a different claim
/// entirely, and the one that hides a missing row.
/// </para>
/// </remarks>
/// <param name="page">How many to take and where to continue from.</param>
/// <param name="operation">The operation being served, as named in the protocol.</param>
/// <param name="request">Everything about the request that decides what the page holds.</param>
/// <param name="what">What is being counted, for the message when the page is cut short.</param>
public sealed class UiPageWindow(UiPageRequest page, string operation, string request, string what)
{
	private readonly UiPageRequest _page = page ?? UiPageRequest.Default;
	private readonly string _operation = operation;
	private readonly string _request = request;
	private readonly string _what = what;
	private readonly long _from = UiPageCursor.StartAt(
		(page ?? UiPageRequest.Default).Cursor, operation, request, 0);

	private long _matched;
	private long _taken;

	/// <summary>
	/// How many matched, whether or not they were returned.
	/// </summary>
	public long Total => _matched;

	/// <summary>
	/// Whether there is more than this page holds.
	/// </summary>
	public bool Truncated { get; private set; }

	/// <summary>
	/// What to send back to continue, or <see langword="null"/> when there is nothing left.
	/// </summary>
	public string NextCursor
		=> Truncated ? UiPageCursor.Create(_operation, _request, _from + _taken) : null;

	/// <summary>
	/// Why the page stopped, or <see langword="null"/> when it did not.
	/// </summary>
	public string TruncationReason
		=> Truncated ? $"Stopped at {_page.Limit} {_what}; ask again with the cursor for the rest." : null;

	/// <summary>
	/// Offers one matching item to the page.
	/// </summary>
	/// <returns><see langword="true"/> when it belongs in this page and should be built.</returns>
	/// <remarks>
	/// Called for every item that matched the query, including the ones before the cursor and the ones
	/// after the limit: that is how the total stays the total rather than becoming the page size.
	/// </remarks>
	public bool Take()
	{
		var position = _matched++;

		if (position < _from)
			return false;

		if (_taken >= _page.Limit)
		{
			Truncated = true;

			return false;
		}

		_taken++;

		return true;
	}

	/// <summary>
	/// Refuses a cursor that belongs to another read.
	/// </summary>
	/// <param name="page">What the caller sent.</param>
	/// <param name="operation">The operation being served.</param>
	/// <param name="request">Everything about the request that decides what the page holds.</param>
	/// <exception cref="ArgumentException">The cursor is not for this read.</exception>
	public static void Check(UiPageRequest page, string operation, string request)
		=> UiPageCursor.StartAt(page?.Cursor, operation, request, 0);
}
