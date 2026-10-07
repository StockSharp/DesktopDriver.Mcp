namespace StockSharp.DesktopDriver.Runtime;

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Input;

/// <summary>
/// The backend for a machine where there is no way to send input.
/// </summary>
/// <remarks>
/// It refuses, and says so in the protocol's own words. The alternative - quietly running the command
/// behind the control instead - would report a passing test for something a person could not do.
/// </remarks>
public sealed class UiNoInputDriver(string why) : IUiInputDriver
{
	private readonly string _why = string.IsNullOrEmpty(why)
		? throw new ArgumentNullException(nameof(why))
		: why;

	/// <inheritdoc />
	public string BackendId => "none";

	/// <inheritdoc />
	public ImmutableArray<string> Capabilities { get; } = [];

	/// <inheritdoc />
	public UiError Refusal => UiError.Create(UiErrorCodes.InputUnavailable, _why);

	/// <inheritdoc />
	public Task<UiActionReceipt> ExecuteAsync(
		UiInputRequest request,
		UiResolvedInputTarget resolvedTarget,
		CancellationToken cancellationToken)
		=> throw new UiAutomationException(Refusal);
}
