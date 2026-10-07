namespace StockSharp.DesktopDriver.Diagnostics;

using System;
using System.Collections.Immutable;

/// <summary>
/// What went wrong, in a form a test can branch on.
/// </summary>
/// <param name="Code">One of <see cref="UiErrorCodes"/>.</param>
/// <param name="Message">What happened, for a person.</param>
/// <param name="ActionId">The action it happened to, when it was an action.</param>
/// <param name="DiagnosticArtifactId">Where to find more, when more was kept.</param>
/// <param name="Details">A few named facts; never a dump of an object or an exception.</param>
public sealed record UiError(
	string Code,
	string Message,
	Guid? ActionId,
	string DiagnosticArtifactId,
	ImmutableDictionary<string, string> Details)
{
	/// <summary>
	/// An error with nothing but a code and a message.
	/// </summary>
	/// <param name="code">One of <see cref="UiErrorCodes"/>.</param>
	/// <param name="message">What happened.</param>
	/// <returns>The error.</returns>
	public static UiError Create(string code, string message)
		=> new(code, message, null, null, ImmutableDictionary<string, string>.Empty);
}
