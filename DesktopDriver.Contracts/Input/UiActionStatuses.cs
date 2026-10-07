namespace StockSharp.DesktopDriver.Input;

/// <summary>
/// How far an action got.
/// </summary>
/// <remarks>
/// None of these says the application did what the action was for. Dispatched means the input was sent;
/// whether an order was placed is a separate question, asked of the interface afterwards.
/// </remarks>
public enum UiActionStatuses
{
	/// <summary>Accepted and waiting its turn.</summary>
	Queued,

	/// <summary>Being resolved and sent.</summary>
	Running,

	/// <summary>The whole sequence reached the input system.</summary>
	Dispatched,

	/// <summary>Refused before anything was sent.</summary>
	FailedBeforeDispatch,

	/// <summary>Cancelled before anything was sent.</summary>
	CancelledBeforeDispatch,

	/// <summary>Part of the sequence was sent.</summary>
	PartiallyDispatched,

	/// <summary>Something was sent and what came of it could not be established.</summary>
	OutcomeUnknown,
}
