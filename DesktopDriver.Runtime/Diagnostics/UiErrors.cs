namespace StockSharp.DesktopDriver.Runtime;

using System;

using StockSharp.DesktopDriver.Diagnostics;

/// <summary>
/// The failures this module raises, each as the protocol's own code.
/// </summary>
/// <remarks>
/// Public because adapters are written in other assemblies and have to refuse in the protocol's own
/// words: an adapter that threw an ordinary exception would reach the caller as an unclassified
/// failure, and a test could only tell what went wrong by reading the message.
/// </remarks>
public static class UiErrors
{
	/// <summary>
	/// Nothing is registered under that address.
	/// </summary>
	/// <param name="what">What exactly happened.</param>
	/// <returns>The exception to throw.</returns>
	public static UiAutomationException NotFound(string what)
		=> Fail(UiErrorCodes.NotFound, what);

	/// <summary>
	/// The handle names a visual that no longer exists.
	/// </summary>
	/// <param name="what">What exactly happened.</param>
	/// <returns>The exception to throw.</returns>
	public static UiAutomationException Stale(string what)
		=> Fail(UiErrorCodes.StaleElement, what);

	/// <summary>
	/// The request itself is malformed or asks for something unknown.
	/// </summary>
	/// <param name="what">What exactly happened.</param>
	/// <returns>The exception to throw.</returns>
	public static UiAutomationException Invalid(string what)
		=> Fail(UiErrorCodes.InvalidRequest, what);

	/// <summary>
	/// Two adapters claim the same control and neither is more specific.
	/// </summary>
	/// <param name="what">What exactly happened.</param>
	/// <returns>The exception to throw.</returns>
	public static UiAutomationException Ambiguous(string what)
		=> Fail(UiErrorCodes.AmbiguousAdapter, what);

	/// <summary>
	/// The control cannot do what was asked of it.
	/// </summary>
	/// <param name="what">What exactly happened.</param>
	/// <returns>The exception to throw.</returns>
	public static UiAutomationException Unsupported(string what)
		=> Fail(UiErrorCodes.UnsupportedCapability, what);

	/// <summary>
	/// Something moved under the read guard.
	/// </summary>
	/// <param name="what">What exactly happened.</param>
	/// <returns>The exception to throw.</returns>
	public static UiAutomationException Changed(string what)
		=> Fail(UiErrorCodes.StateChanged, what);

	/// <summary>
	/// An action identifier was reused for something else.
	/// </summary>
	/// <param name="what">What exactly happened.</param>
	/// <returns>The exception to throw.</returns>
	public static UiAutomationException Conflict(string what)
		=> Fail(UiErrorCodes.ActionConflict, what);

	/// <summary>
	/// A limit of the protocol or the session was reached.
	/// </summary>
	/// <param name="what">What exactly happened.</param>
	/// <returns>The exception to throw.</returns>
	public static UiAutomationException LimitExceeded(string what)
		=> Fail(UiErrorCodes.LimitExceeded, what);

	/// <summary>
	/// The operation ran out of time.
	/// </summary>
	/// <param name="what">What exactly happened.</param>
	/// <returns>The exception to throw.</returns>
	public static UiAutomationException TimedOut(string what)
		=> Fail(UiErrorCodes.Timeout, what);

	/// <summary>
	/// A failure with any of the protocol's codes.
	/// </summary>
	/// <param name="code">One of <see cref="UiErrorCodes"/>.</param>
	/// <param name="message">What exactly happened.</param>
	/// <returns>The exception to throw.</returns>
	public static UiAutomationException Fail(string code, string message)
		=> new(UiError.Create(code, message));
}
