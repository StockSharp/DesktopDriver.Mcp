namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Runs a test body on the thread the headless session owns.
/// </summary>
internal static class HeadlessRun
{
	/// <summary>
	/// Runs an asynchronous body and waits for it.
	/// </summary>
	/// <param name="body">The body.</param>
	/// <returns>A task that completes when the body has finished, carrying whatever it threw.</returns>
	/// <remarks>
	/// The body goes over as a lambda the session can see is asynchronous rather than as a delegate: given
	/// a delegate the session runs it, takes back the task it returned and never waits on it, so the
	/// interface thread stops pumping the moment the body first awaits and a failed assertion is never
	/// seen at all.
	/// </remarks>
	public static Task OnUiThread(Func<Task> body)
		=> AssemblyInitializer.Session.Dispatch(async () =>
		{
			await body();
			return true;
		}, CancellationToken.None);

	/// <summary>
	/// Runs a body and waits for it.
	/// </summary>
	/// <param name="body">The body.</param>
	/// <returns>A task that completes when the body has finished.</returns>
	public static Task OnUiThread(Action body)
		=> AssemblyInitializer.Session.Dispatch(body, CancellationToken.None);
}
