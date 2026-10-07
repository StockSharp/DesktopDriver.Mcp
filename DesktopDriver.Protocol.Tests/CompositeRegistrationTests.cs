namespace StockSharp.DesktopDriver.Tests.Protocol;

using System;
using System.Collections.Generic;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Host;

/// <summary>
/// The modules of a driven application are unregistered together, and a failure among them is not lost.
/// </summary>
[TestClass]
public class CompositeRegistrationTests : BaseTestClass
{
	[TestMethod]
	public void RegistrationsAreUndoneInReverseOrder()
	{
		var undone = new List<string>();

		new UiCompositeRegistration(new Undo(() => undone.Add("first")), new Undo(() => undone.Add("second"))).Dispose();

		AreEqual("second,first", string.Join(",", undone));
	}

	[TestMethod]
	public void OneThatCannotBeUndoneLeavesNoOtherRegisteredAndIsReported()
	{
		var undone = new List<string>();
		var failure = new InvalidOperationException("cannot unregister");

		var composite = new UiCompositeRegistration(
			new Undo(() => undone.Add("first")),
			new Undo(() => throw failure),
			new Undo(() => undone.Add("third")));

		var error = Throws<AggregateException>(composite.Dispose);

		AreEqual("third,first", string.Join(",", undone));
		AreSame(failure, error.InnerExceptions[0]);
	}

	private sealed class Undo(Action action) : IDisposable
	{
		public void Dispose() => action();
	}
}
