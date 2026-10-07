namespace StockSharp.DesktopDriver.Diagnostics;

/// <summary>
/// The failures the protocol distinguishes.
/// </summary>
/// <remarks>
/// A test decides what to do from the code, never from the message: messages are written for people and
/// get rewritten, and a test that matched on one would fail the day somebody improved the wording.
/// </remarks>
public static class UiErrorCodes
{
	/// <summary>The connection has not completed the session handshake.</summary>
	public const string Unauthorized = "unauthorized";

	/// <summary>The two sides do not speak the same version of the protocol.</summary>
	public const string ProtocolMismatch = "protocolMismatch";

	/// <summary>The request itself is malformed or asks for something unknown.</summary>
	public const string InvalidRequest = "invalidRequest";

	/// <summary>No such node.</summary>
	public const string NotFound = "notFound";

	/// <summary>The selector matched more than one node.</summary>
	public const string AmbiguousElement = "ambiguousElement";

	/// <summary>Two adapters claim the same control and neither is more specific.</summary>
	public const string AmbiguousAdapter = "ambiguousAdapter";

	/// <summary>The handle names a visual that no longer exists.</summary>
	public const string StaleElement = "staleElement";

	/// <summary>The node exists in the model but has no visual yet.</summary>
	public const string NotCreated = "notCreated";

	/// <summary>The node exists but would not take this input.</summary>
	public const string NotInteractable = "notInteractable";

	/// <summary>The node cannot do what was asked of it.</summary>
	public const string UnsupportedCapability = "unsupportedCapability";

	/// <summary>Something changed under the read guard, so the answer would mix two states.</summary>
	public const string StateChanged = "stateChanged";

	/// <summary>The operation ran out of time.</summary>
	public const string Timeout = "timeout";

	/// <summary>The caller cancelled.</summary>
	public const string Cancelled = "cancelled";

	/// <summary>The interface could not be read at all.</summary>
	public const string UiUnavailable = "uiUnavailable";

	/// <summary>There is no way to send input in this environment.</summary>
	public const string InputUnavailable = "inputUnavailable";

	/// <summary>Something outside the application is holding the input system.</summary>
	public const string InputBlocked = "inputBlocked";

	/// <summary>A limit of the protocol or the session was reached.</summary>
	public const string LimitExceeded = "limitExceeded";

	/// <summary>The application is gone.</summary>
	public const string ApplicationExited = "applicationExited";

	/// <summary>The same action identifier was reused with different parameters.</summary>
	public const string ActionConflict = "actionConflict";

	/// <summary>Input was sent and what came of it could not be established.</summary>
	public const string OutcomeUnknown = "outcomeUnknown";

	/// <summary>No such artifact.</summary>
	public const string ArtifactNotFound = "artifactNotFound";

	/// <summary>The artifact was there and has been cleaned up.</summary>
	public const string ArtifactExpired = "artifactExpired";
}
