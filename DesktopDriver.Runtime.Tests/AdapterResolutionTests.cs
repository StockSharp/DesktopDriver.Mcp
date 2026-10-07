namespace StockSharp.DesktopDriver.Tests;

using System;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// Which adapter answers for a control, and when the answer would be an accident.
/// </summary>
[TestClass]
public class AdapterResolutionTests : BaseTestClass
{
	private static UiSubject Subject(object instance) => new(new UiNodeId("scope", "node"), instance);

	[TestMethod]
	public void TheAdapterForTheExactTypeBeatsTheOneForItsBase()
	{
		var registry = new UiAdapterRegistry();
		var baseAdapter = new StubAdapter("base", typeof(TestControl));
		var exact = new StubAdapter("exact", typeof(DerivedControl));

		registry.Register("base", typeof(TestControl), baseAdapter);
		registry.Register("exact", typeof(DerivedControl), exact);

		AreSame(exact, registry.Resolve(Subject(new DerivedControl())));
		AreSame(baseAdapter, registry.Resolve(Subject(new TestControl())));
	}

	[TestMethod]
	public void PriorityBeatsSpecificity()
	{
		// A product that has to override a shared adapter says so with a priority, rather than by
		// declaring a more derived type it does not have.
		var registry = new UiAdapterRegistry();
		var exact = new StubAdapter("exact", typeof(DerivedControl));
		var general = new StubAdapter("general", typeof(TestControl));

		registry.Register("exact", typeof(DerivedControl), exact);
		registry.Register("general", typeof(TestControl), general, priority: 10);

		AreSame(general, registry.Resolve(Subject(new DerivedControl())));
	}

	[TestMethod]
	public void TwoUnrelatedInterfacesThatBothClaimAControlAreAConflict()
	{
		var registry = new UiAdapterRegistry();

		registry.Register("first", typeof(IFirstThing), new StubAdapter("first", typeof(IFirstThing)));
		registry.Register("second", typeof(ISecondThing), new StubAdapter("second", typeof(ISecondThing)));

		var error = Throws<UiAutomationException>(() => registry.Resolve(Subject(new BothThings())));

		AreEqual(UiErrorCodes.AmbiguousAdapter, error.Error.Code);
	}

	[TestMethod]
	public void AnInterfaceThatExtendsTheOthersIsNotAConflict()
	{
		var registry = new UiAdapterRegistry();
		var both = new StubAdapter("both", typeof(IBothThings));

		registry.Register("first", typeof(IFirstThing), new StubAdapter("first", typeof(IFirstThing)));
		registry.Register("both", typeof(IBothThings), both);

		AreSame(both, registry.Resolve(Subject(new BothThings())));
	}

	[TestMethod]
	public void AnAdapterThatDeclinesIsNotConsidered()
	{
		var registry = new UiAdapterRegistry();
		var declining = new StubAdapter("declining", typeof(TestControl)) { Claims = false };

		registry.Register("declining", typeof(TestControl), declining);

		IsNull(registry.Resolve(Subject(new TestControl())));
	}

	[TestMethod]
	public void ReleasingOneOwnerOfASharedRegistrationLeavesItInPlace()
	{
		// Two modules may both depend on a third and both register its adapters. The first of them to be
		// unloaded must not take the other's adapter with it.
		var registry = new UiAdapterRegistry();
		var adapter = new StubAdapter("shared", typeof(TestControl));

		var first = registry.Register("shared", typeof(TestControl), adapter);
		var second = registry.Register("shared", typeof(TestControl), adapter);

		first.Dispose();
		AreSame(adapter, registry.Resolve(Subject(new TestControl())));

		second.Dispose();
		IsNull(registry.Resolve(Subject(new TestControl())));
	}

	[TestMethod]
	public void ReusingAnAdapterIdentityForSomethingElseIsRefused()
	{
		var registry = new UiAdapterRegistry();

		registry.Register("name", typeof(TestControl), new StubAdapter("a", typeof(TestControl)));

		Throws<InvalidOperationException>(() =>
			registry.Register("name", typeof(DerivedControl), new StubAdapter("b", typeof(DerivedControl))));
	}
}
