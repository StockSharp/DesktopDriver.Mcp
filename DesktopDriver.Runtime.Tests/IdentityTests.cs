namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Runtime.CompilerServices;

using Ecng.UnitTesting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// What an address means over time, and what a handle stops meaning.
/// </summary>
[TestClass]
public class IdentityTests : BaseTestClass
{
	private static readonly UiNodeId _id = new("panel:orders", "orders-grid");

	[TestMethod]
	public void TheSameControlRegisteredAgainKeepsItsHandle()
	{
		// A panel that is merely moved must not invalidate references the caller already holds.
		var registry = new UiNodeRegistry(Guid.NewGuid());
		var control = new TestControl();

		var first = registry.Register(new UiSubject(_id, control), "grid", true);
		var second = registry.Register(new UiSubject(_id, control), "grid", true);

		AreEqual(first.Handle, second.Handle);
		AreEqual(1, second.Handle.Generation);
	}

	[TestMethod]
	public void ARebuiltControlTakesANewHandleAndTheOldOneStopsWorking()
	{
		var registry = new UiNodeRegistry(Guid.NewGuid());

		var first = registry.Register(new UiSubject(_id, new TestControl()), "grid", true);
		var second = registry.Register(new UiSubject(_id, new TestControl()), "grid", true);

		AreNotEqual(first.Handle, second.Handle);
		AreEqual(2, second.Handle.Generation);

		// The address still means the same panel...
		IsNotNull(registry.Resolve(UiTarget.FromId(_id)));

		// ...but the caller asked for the visual that is gone.
		var error = Throws<UiAutomationException>(() => registry.Resolve(UiTarget.FromHandle(first.Handle)));
		AreEqual(UiErrorCodes.StaleElement, error.Error.Code);
	}

	[TestMethod]
	public void AnAddressWhoseControlLeftTheScreenMeansTheOneThatReplacedIt()
	{
		// A page rebuilt under the same address - navigated away from and back to - is a new visual, and the
		// one registered first can no longer be drawn or pressed.
		var registry = new UiNodeRegistry(Guid.NewGuid());
		var gone = new TestControl { Name = "gone" };
		var current = new TestControl { Name = "current" };

		registry.Register(new UiSubject(_id, gone), "page", true);
		registry.IsLive = instance => !ReferenceEquals(instance, gone);
		registry.Locator = new FixedLocator(new UiSubject(_id, current));

		AreSame(current, registry.Resolve(UiTarget.FromId(_id)).Instance);
	}

	[TestMethod]
	public void AnAddressWhoseControlIsStillOnScreenIsNotSearchedFor()
	{
		var registry = new UiNodeRegistry(Guid.NewGuid());
		var control = new TestControl();

		registry.Register(new UiSubject(_id, control), "page", true);
		registry.IsLive = _ => true;
		registry.Locator = new FixedLocator(new UiSubject(_id, new TestControl()));

		AreSame(control, registry.Resolve(UiTarget.FromId(_id)).Instance);
	}

	[TestMethod]
	public void AControlThatLeftTheScreenAndWasNotReplacedIsStillWhatTheAddressMeans()
	{
		// Nothing newer to offer, so the caller is told about the one there is - and the backend then says
		// precisely why it cannot be reached.
		var registry = new UiNodeRegistry(Guid.NewGuid());
		var gone = new TestControl();

		registry.Register(new UiSubject(_id, gone), "page", true);
		registry.IsLive = _ => false;
		registry.Locator = new FixedLocator(null);

		AreSame(gone, registry.Resolve(UiTarget.FromId(_id)).Instance);
	}

	[TestMethod]
	public void AnAddressNobodyRegisteredIsNotFound()
	{
		var registry = new UiNodeRegistry(Guid.NewGuid());

		var error = Throws<UiAutomationException>(() => registry.Resolve(UiTarget.FromId(_id)));

		AreEqual(UiErrorCodes.NotFound, error.Error.Code);
	}

	[TestMethod]
	public void ATargetThatNamesANodeTwoWaysIsRefused()
	{
		var registry = new UiNodeRegistry(Guid.NewGuid());
		var handle = registry.Register(new UiSubject(_id, new TestControl()), "grid", true).Handle;

		var error = Throws<UiAutomationException>(() => registry.Resolve(new UiTarget(_id, handle)));

		AreEqual(UiErrorCodes.InvalidRequest, error.Error.Code);
	}

	[TestMethod]
	public void ANodeWhoseVisualDoesNotExistYetHasNoHandle()
	{
		var registry = new UiNodeRegistry(Guid.NewGuid());

		var reference = registry.Register(new UiSubject(_id, new TestControl()), "panel", visualCreated: false);

		IsNull(reference.Handle, "A handle names a visual, and there is no visual yet.");
		IsNotNull(registry.Resolve(UiTarget.FromId(_id)));
	}

	[TestMethod]
	public void ForgettingANodeForgetsItsHandleToo()
	{
		var registry = new UiNodeRegistry(Guid.NewGuid());
		var handle = registry.Register(new UiSubject(_id, new TestControl()), "grid", true).Handle;

		registry.Unregister(_id);

		IsNull(registry.GetReference(_id));
		AreEqual(UiErrorCodes.StaleElement,
			Throws<UiAutomationException>(() => registry.Resolve(UiTarget.FromHandle(handle))).Error.Code);
	}

	[TestMethod]
	public void AClosedWindowIsNotHeldAliveByTheRegistry()
	{
		var registry = new UiNodeRegistry(Guid.NewGuid());
		var reference = Register(registry);

		for (var attempt = 0; attempt < 5 && reference.IsAlive; attempt++)
		{
			GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
			GC.WaitForPendingFinalizers();
		}

		IsFalse(reference.IsAlive, "The registry kept a control the application had finished with.");
		AreEqual(1, registry.Prune());

		// The control never appears in this method's own frame: a debug build keeps locals alive to the
		// end of the method, which would make the test pass or fail on the build configuration.
		[MethodImpl(MethodImplOptions.NoInlining)]
		static WeakReference Register(UiNodeRegistry registry)
		{
			var control = new TestControl();
			registry.Register(new UiSubject(_id, control), "grid", true);

			return new WeakReference(control);
		}
	}

	private sealed class FixedLocator(UiSubject subject) : IUiNodeLocator
	{
		public UiSubject Locate(UiNodeId id) => subject;
	}
}
