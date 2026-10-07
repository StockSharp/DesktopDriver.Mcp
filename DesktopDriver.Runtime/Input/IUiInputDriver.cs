namespace StockSharp.DesktopDriver.Runtime;

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Input;

/// <summary>
/// Sends real input.
/// </summary>
/// <remarks>
/// The one place in the module that touches the input system, and the reason the rest of it can claim
/// to test an interface: everything above this decides what a person would do, and this does it.
/// </remarks>
public interface IUiInputDriver
{
	/// <summary>
	/// Which backend this is, as it appears in a receipt.
	/// </summary>
	string BackendId { get; }

	/// <summary>
	/// What this backend can send.
	/// </summary>
	ImmutableArray<string> Capabilities { get; }

	/// <summary>
	/// Why this backend refuses every action, or <see langword="null"/> when it delivers them.
	/// </summary>
	/// <remarks>
	/// A run that may not be driven refuses input whoever would carry it out: a control that performs its
	/// own input is not offered an action this answers for.
	/// </remarks>
	UiError Refusal { get; }

	/// <summary>
	/// Sends one action.
	/// </summary>
	/// <param name="request">What to do.</param>
	/// <param name="resolvedTarget">Where it will land.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>How far it got.</returns>
	/// <remarks>
	/// Releasing whatever it pressed is the driver's responsibility on every path out, including the
	/// failing ones: a test that stopped half way must not leave the desktop holding a mouse button.
	/// </remarks>
	Task<UiActionReceipt> ExecuteAsync(
		UiInputRequest request,
		UiResolvedInputTarget resolvedTarget,
		CancellationToken cancellationToken);
}
