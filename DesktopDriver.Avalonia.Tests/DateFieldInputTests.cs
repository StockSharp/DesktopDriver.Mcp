namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia.Controls;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// A date written into a field that holds a date.
/// </summary>
/// <remarks>
/// A calendar field is a box a person types a date into, and a case that writes one has to land on the day
/// the person would have typed - in the reader's own format first.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class DateFieldInputTests : BaseTestClass
{
	private static Task RunAsync(Action body) => AssemblyInitializer.Session.Dispatch(body, CancellationToken.None);

	[TestMethod]
	[Timeout(60000)]
	public Task ADateTypedIntoACalendarFieldIsTheDateItHolds() => RunAsync(() =>
	{
		var culture = CultureInfo.CurrentCulture;

		try
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");

			var field = new CalendarDatePicker { Name = "From" };

			using var harness = new ControlHarness(field);

			var adapter = (IUiInputPerformingAdapter)harness.Adapter;
			var action = new UiTextAction("01.04.2025", UiTextModes.Replace);

			IsTrue(adapter.CanPerform(harness.Subject, UiControlPart.Instance, action));

			adapter.Perform(harness.Subject, UiControlPart.Instance, action);

			AreEqual(new DateTime(2025, 4, 1), field.SelectedDate);
		}
		finally
		{
			CultureInfo.CurrentCulture = culture;
		}
	});
}
