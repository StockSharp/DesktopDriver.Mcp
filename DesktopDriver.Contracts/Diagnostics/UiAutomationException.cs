namespace StockSharp.DesktopDriver.Diagnostics;

using System;

/// <summary>
/// The exception a client throws when the other side returned an error.
/// </summary>
/// <remarks>
/// It carries the typed error rather than only a message, so a test branches on
/// <see cref="UiError.Code"/> instead of matching text that is free to change.
/// </remarks>
public class UiAutomationException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UiAutomationException"/> class.
	/// </summary>
	/// <param name="error">What went wrong.</param>
	/// <param name="innerException">The local cause, when there was one.</param>
	public UiAutomationException(UiError error, Exception innerException = null)
		: base(error?.Message ?? string.Empty, innerException)
	{
		Error = error ?? throw new ArgumentNullException(nameof(error));
	}

	/// <summary>
	/// What went wrong.
	/// </summary>
	public UiError Error { get; }
}
