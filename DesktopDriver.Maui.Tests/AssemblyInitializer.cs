namespace StockSharp.DesktopDriver.Tests.Maui;

using System;
using System.Threading.Tasks;

using Microsoft.Maui.Dispatching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Gives the controls these tests make a thread to belong to.
/// </summary>
/// <remarks>
/// A MAUI control asks for the dispatcher of the thread it is made on, and a test thread has none: a list or a
/// tabbed page refuses to be made without one. Everything here runs where it is called, which is what the one
/// thread of an application amounts to for a test that does all its work on it.
/// </remarks>
[TestClass]
public static class AssemblyInitializer
{
	/// <summary>
	/// Installs the dispatcher.
	/// </summary>
	/// <param name="context">The test context.</param>
	[AssemblyInitialize]
	public static void Initialize(TestContext context)
		=> DispatcherProvider.SetCurrent(new InlineDispatcherProvider());

	private sealed class InlineDispatcherProvider : IDispatcherProvider
	{
		public IDispatcher GetForCurrentThread() => InlineDispatcher.Instance;
	}

	private sealed class InlineDispatcher : IDispatcher
	{
		public static InlineDispatcher Instance { get; } = new();

		public bool IsDispatchRequired => false;

		public bool Dispatch(Action action)
		{
			action();

			return true;
		}

		public bool DispatchDelayed(TimeSpan delay, Action action)
		{
			_ = Task.Delay(delay).ContinueWith(_ => action(), TaskScheduler.Default);

			return true;
		}

		public IDispatcherTimer CreateTimer()
			=> throw new NotSupportedException("Nothing tested here runs on a timer.");
	}
}
