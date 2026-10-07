namespace StockSharp.DesktopDriver.Cli;

using StockSharp.DesktopDriver.Diagnostics;

/// <summary>
/// What this program's exit code means.
/// </summary>
/// <remarks>
/// Fixed, because a script decides what to do from the number. A caller that had to read the message to
/// tell "the application refused this" from "the application is not there" would break the day either
/// message was reworded.
/// <para>
/// Nought means the operation ran, and nothing more. A click that was delivered says nought whether or
/// not the button did what the caller hoped; that is a separate question, asked with a read.
/// </para>
/// </remarks>
public static class UiExitCodes
{
	/// <summary>The operation ran.</summary>
	public const int Ok = 0;

	/// <summary>Something unexpected went wrong.</summary>
	public const int Failed = 1;

	/// <summary>The command line is wrong.</summary>
	public const int Usage = 2;

	/// <summary>The application could not be reached.</summary>
	public const int Unreachable = 3;

	/// <summary>The application refused the operation.</summary>
	public const int Refused = 4;

	/// <summary>
	/// Which code a refusal from the application comes out as.
	/// </summary>
	/// <param name="code">The protocol's own code, from <see cref="UiErrorCodes"/>.</param>
	/// <returns>One of the codes above.</returns>
	/// <remarks>
	/// Refused means the application answered and said no; unreachable means there was nobody to answer.
	/// A script retries the second and gives up on the first, so the two never share a number.
	/// <para>
	/// Running out of time, being cancelled and not knowing what became of an input are none of the
	/// three: nothing about them says whether asking again would help, so they come out as an ordinary
	/// failure rather than as an invitation to retry.
	/// </para>
	/// </remarks>
	public static int For(string code)
		=> code switch
		{
			UiErrorCodes.ApplicationExited or
			UiErrorCodes.UiUnavailable or
			UiErrorCodes.ProtocolMismatch => Unreachable,

			UiErrorCodes.Unauthorized or
			UiErrorCodes.InvalidRequest or
			UiErrorCodes.NotFound or
			UiErrorCodes.AmbiguousElement or
			UiErrorCodes.AmbiguousAdapter or
			UiErrorCodes.StaleElement or
			UiErrorCodes.NotCreated or
			UiErrorCodes.NotInteractable or
			UiErrorCodes.UnsupportedCapability or
			UiErrorCodes.StateChanged or
			UiErrorCodes.InputUnavailable or
			UiErrorCodes.InputBlocked or
			UiErrorCodes.LimitExceeded or
			UiErrorCodes.ActionConflict or
			UiErrorCodes.ArtifactNotFound or
			UiErrorCodes.ArtifactExpired => Refused,

			_ => Failed,
		};
}
