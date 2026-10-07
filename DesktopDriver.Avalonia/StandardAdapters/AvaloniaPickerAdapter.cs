namespace StockSharp.DesktopDriver.Avalonia;

using System;
using System.Collections.Immutable;
using System.Globalization;

using global::Avalonia.Controls;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// Writes into a picker of a date or a time.
/// </summary>
/// <remarks>
/// A picker holds a date rather than the text it shows: the day, the month and the year are three
/// separate spinners, and there is nothing to type into. Writing a date therefore left it unchanged,
/// and a case that then asked for data got whatever range the product happened to start with.
/// <para>
/// The value is set instead, which is what the picker itself arrives at when a person has turned the
/// three spinners - and it is read in the product's own language first, so that a case reads the way
/// the picker shows it.
/// </para>
/// </remarks>
public sealed class AvaloniaPickerAdapter(AvaloniaNodeBinder binder)
	: AvaloniaControlAdapter(binder), IUiInputPerformingAdapter
{
	/// <inheritdoc />
	public override string Kind => "datePicker";

	/// <inheritdoc />
	public override bool CanHandle(UiSubject subject) => subject?.Instance is DatePicker or CalendarDatePicker or TimePicker;

	/// <inheritdoc />
	public override ImmutableArray<string> GetCapabilities(UiSubject subject)
		=> ["input.click", "input.key", "input.text"];

	/// <inheritdoc />
	public bool CanPerform(UiSubject subject, UiTargetPart part, UiInputAction action)
		=> CanHandle(subject) && part is UiControlPart && action is UiTextAction;

	/// <inheritdoc />
	public void Perform(UiSubject subject, UiTargetPart part, UiInputAction action)
	{
		ArgumentNullException.ThrowIfNull(subject);

		var text = ((UiTextAction)action).Text;

		switch (subject.Instance)
		{
			case DatePicker date:
				date.SelectedDate = string.IsNullOrEmpty(text)
					? null
					: new DateTimeOffset(Parse(text).Date, TimeSpan.Zero);

				break;

			case CalendarDatePicker calendar:
				calendar.SelectedDate = string.IsNullOrEmpty(text) ? null : Parse(text).Date;

				break;

			case TimePicker time:
				time.SelectedTime = string.IsNullOrEmpty(text) ? null : Parse(text).TimeOfDay;

				break;
		}
	}

	// The product's own language first, because that is how the picker shows what it holds and so how a
	// case would write it down; then the unambiguous form, so that a case which has to run under any
	// language can write a date once and have it mean one day everywhere.
	private static DateTime Parse(string text)
	{
		foreach (var culture in new[] { CultureInfo.CurrentCulture, CultureInfo.InvariantCulture })
		{
			if (DateTime.TryParse(text, culture, DateTimeStyles.None, out var parsed))
				return parsed;
		}

		throw UiErrors.Invalid($"'{text}' is not a date this picker can be set to.");
	}
}
