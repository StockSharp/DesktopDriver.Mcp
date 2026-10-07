namespace StockSharp.DesktopDriver.Runtime;

using System;
using System.Collections.Generic;
using System.Threading;

using StockSharp.DesktopDriver.Input;

/// <summary>
/// What each action was, and what became of it.
/// </summary>
/// <remarks>
/// This is what makes a dropped connection safe. A caller that did not hear the answer asks again with
/// the same identifier and is told what happened, instead of sending a second click that a person would
/// see. Asking again with different parameters is a mistake in the caller and is refused.
/// <para>
/// The journal is bounded, and it refuses new actions rather than forgetting old ones: a forgotten
/// identifier could be replayed, which is the one thing the journal exists to prevent.
/// </para>
/// </remarks>
public sealed class UiActionJournal
{
	private sealed class Record
	{
		public string Fingerprint;
		public UiActionReceipt Receipt;
	}

	private readonly Lock _sync = new();
	private readonly Dictionary<Guid, Record> _records = [];
	private readonly int _capacity;

	/// <summary>
	/// Initializes a new instance of the <see cref="UiActionJournal"/> class.
	/// </summary>
	/// <param name="capacity">How many actions one session may perform.</param>
	public UiActionJournal(int capacity = 10000)
	{
		if (capacity <= 0)
			throw new ArgumentOutOfRangeException(nameof(capacity));

		_capacity = capacity;
	}

	/// <summary>
	/// How many actions this session has performed.
	/// </summary>
	public int Count
	{
		get
		{
			using (_sync.EnterScope())
				return _records.Count;
		}
	}

	/// <summary>
	/// Claims an action identifier before anything is sent.
	/// </summary>
	/// <param name="request">The action.</param>
	/// <param name="existing">What became of it last time, when it has been seen before.</param>
	/// <returns><see langword="true"/> when this is the first time and the caller should go ahead.</returns>
	public bool TryClaim(UiInputRequest request, out UiActionReceipt existing)
	{
		ArgumentNullException.ThrowIfNull(request);

		var fingerprint = Fingerprint(request);

		using (_sync.EnterScope())
		{
			if (_records.TryGetValue(request.ActionId, out var record))
			{
				if (record.Fingerprint != fingerprint)
					throw UiErrors.Conflict($"Action {request.ActionId} was already used for something else.");

				existing = record.Receipt;
				return false;
			}

			if (_records.Count >= _capacity)
				throw UiErrors.LimitExceeded($"This session has performed its {_capacity} actions.");

			_records.Add(request.ActionId, new Record { Fingerprint = fingerprint });
			existing = null;

			return true;
		}
	}

	/// <summary>
	/// Records what became of an action.
	/// </summary>
	/// <param name="receipt">The outcome.</param>
	public void Complete(UiActionReceipt receipt)
	{
		ArgumentNullException.ThrowIfNull(receipt);

		using (_sync.EnterScope())
		{
			if (_records.TryGetValue(receipt.ActionId, out var record))
				record.Receipt = receipt;
		}
	}

	/// <summary>
	/// Looks up what became of an action.
	/// </summary>
	/// <param name="actionId">The identifier.</param>
	/// <returns>The outcome, or <see langword="null"/> when the action is unknown.</returns>
	public UiActionReceipt Find(Guid actionId)
	{
		using (_sync.EnterScope())
			return _records.TryGetValue(actionId, out var record) ? record.Receipt : null;
	}

	private static string Fingerprint(UiInputRequest request)
		=> $"{request.Target}|{request.Part}|{request.Action}";
}
