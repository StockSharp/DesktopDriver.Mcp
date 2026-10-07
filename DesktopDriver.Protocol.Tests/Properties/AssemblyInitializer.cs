namespace StockSharp.DesktopDriver.Tests.Protocol;

using System;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia.Headless;

using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Owns the headless Avalonia session these tests drive.
/// </summary>
[TestClass]
public static class AssemblyInitializer
{
	private static HeadlessUnitTestSession _session;

	/// <summary>
	/// The session.
	/// </summary>
	public static HeadlessUnitTestSession Session => _session
		?? throw new InvalidOperationException("The headless session has not been started.");

	/// <summary>
	/// Starts it.
	/// </summary>
	/// <param name="context">The test context.</param>
	[AssemblyInitialize]
	public static void Initialize(TestContext context)
		=> _session = HeadlessUnitTestSession.StartNew(typeof(TestApplication), AvaloniaTestIsolationLevel.PerAssembly);

	/// <summary>
	/// Stops it.
	/// </summary>
	/// <returns>A task that completes when it is stopped.</returns>
	[AssemblyCleanup]
	public static async Task Cleanup()
	{
		var session = Interlocked.Exchange(ref _session, null);

		if (session is not null)
			await Task.Run(session.Dispose);
	}
}
