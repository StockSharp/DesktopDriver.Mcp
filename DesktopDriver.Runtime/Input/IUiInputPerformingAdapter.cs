namespace StockSharp.DesktopDriver.Input;

using StockSharp.DesktopDriver.Adapters;

/// <summary>
/// Carries out an action itself, for a control that cannot be operated by pointing at it.
/// </summary>
/// <remarks>
/// Ordinarily an adapter only says where a part is and the input backend does the rest, which is the
/// honest order: what the product receives is then the same thing a person's click produces.
/// <para>
/// Some control families draw their parts without giving each one a control of its own - a lightweight
/// bar, a virtualized strip - and work out what was clicked from where the pointer is. Nothing here moves
/// a pointer, so such a part can be found, measured and photographed, and still not be pressable by any
/// event sent at it. For those the family's own way of carrying the action out is what a click means, and
/// this is where it is offered.
/// </para>
/// <para>
/// Offered rather than preferred: this is asked only after the part has been resolved, so a part that is
/// missing, covered or disabled is still refused first, and a family that does not need this is
/// unaffected.
/// </para>
/// </remarks>
public interface IUiInputPerformingAdapter
{
	/// <summary>
	/// Whether this adapter has to carry the action out itself.
	/// </summary>
	/// <param name="subject">The control.</param>
	/// <param name="part">The part of it the action names.</param>
	/// <param name="action">The action.</param>
	/// <returns><see langword="true"/> when it does.</returns>
	bool CanPerform(UiSubject subject, UiTargetPart part, UiInputAction action);

	/// <summary>
	/// Carries the action out.
	/// </summary>
	/// <param name="subject">The control.</param>
	/// <param name="part">The part of it the action names.</param>
	/// <param name="action">The action.</param>
	void Perform(UiSubject subject, UiTargetPart part, UiInputAction action);
}
