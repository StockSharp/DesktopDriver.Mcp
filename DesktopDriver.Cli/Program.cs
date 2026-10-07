namespace StockSharp.DesktopDriver.Cli;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Runner;
using StockSharp.DesktopDriver.Serialization;

/// <summary>
/// The command line.
/// </summary>
/// <remarks>
/// The answer goes to standard output and nothing else does, so a caller can pipe it into a JSON reader
/// without filtering anything out of it first.
/// </remarks>
public static class Program
{
	/// <summary>
	/// Runs one operation.
	/// </summary>
	/// <param name="args">The command line.</param>
	/// <returns>One of <see cref="UiExitCodes"/>.</returns>
	public static async Task<int> Main(string[] args)
	{
		using var cancellation = new CancellationTokenSource();

		Console.CancelKeyPress += (_, e) =>
		{
			// Cancelled rather than killed, so the application is told to stop waiting on us.
			e.Cancel = true;
			cancellation.Cancel();
		};

		try
		{
			if (args is null || args.Length == 0)
				throw new UiUsageException("Say which operation to run.");

			if (args[0] is "help" or "--help" or "-h")
			{
				Console.Out.WriteLine(UiUsage.Text);

				return UiExitCodes.Ok;
			}

			var answer = await UiCommands.RunAsync(
				args[0], UiArguments.Parse(args[1..]), cancellation.Token);

			Console.Out.WriteLine(answer);

			return UiExitCodes.Ok;
		}
		catch (UiUsageException error)
		{
			Console.Error.WriteLine(error.Message);
			Console.Error.WriteLine();
			Console.Error.WriteLine(UiUsage.Text);

			return UiExitCodes.Usage;
		}
		catch (UiAutomationException error)
		{
			// The whole error, because its code is what a script decides from and its details say which
			// node or which action it was about.
			Console.Error.WriteLine(UiJson.Write(error.Error));

			return UiExitCodes.For(error.Error.Code);
		}
		catch (UiUnreachableException error)
		{
			Console.Error.WriteLine(error.Message);

			return UiExitCodes.Unreachable;
		}
		catch (Exception error) when (error is IOException or TimeoutException or UnauthorizedAccessException)
		{
			Console.Error.WriteLine(error.Message);

			return UiExitCodes.Unreachable;
		}
		catch (OperationCanceledException)
		{
			Console.Error.WriteLine("Stopped.");

			return UiExitCodes.Failed;
		}
		catch (Exception error)
		{
			Console.Error.WriteLine(error.ToString());

			return UiExitCodes.Failed;
		}
	}
}
