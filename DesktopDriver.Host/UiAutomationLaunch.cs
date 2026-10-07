namespace StockSharp.DesktopDriver.Host;

using System;
using System.Linq;

using StockSharp.DesktopDriver.Protocol;

/// <summary>
/// What a runner handed an application it started to be driven.
/// </summary>
/// <remarks>
/// An application reads it once, at start-up, and hands it to the bootstrap of its interface toolkit:
/// <code>
/// if (UiAutomationLaunch.TryRead(args) is { } launch)
///     runtime = AvaloniaAutomationBootstrap.Start(startup, launch.EndpointFile, modules);
/// </code>
/// </remarks>
public sealed class UiAutomationLaunch
{
	private UiAutomationLaunch(string endpointFile)
	{
		EndpointFile = endpointFile;
	}

	/// <summary>
	/// Where to write the endpoint once it is open, or <see langword="null"/> when the runner did not ask.
	/// </summary>
	public string EndpointFile { get; }

	/// <summary>
	/// Reads the launch, if the application was started to be driven.
	/// </summary>
	/// <param name="args">The arguments the application was started with.</param>
	/// <returns>The launch, or <see langword="null"/> when the application was started the ordinary way.</returns>
	public static UiAutomationLaunch TryRead(string[] args)
	{
		if (args is null || !args.Contains(UiLaunchProtocol.EnableSwitch, StringComparer.Ordinal))
			return null;

		var endpoint = args.FirstOrDefault(argument => argument.StartsWith(UiLaunchProtocol.EndpointSwitch, StringComparison.Ordinal));

		return new(endpoint?[UiLaunchProtocol.EndpointSwitch.Length..]);
	}

	/// <summary>
	/// The arguments with the driver's switches taken out.
	/// </summary>
	/// <param name="args">The arguments the application was started with.</param>
	/// <returns>What is left for the application's own parser.</returns>
	public static string[] Strip(string[] args)
		=> args is null
			? []
			: [.. args.Where(argument => argument != UiLaunchProtocol.EnableSwitch && !argument.StartsWith(UiLaunchProtocol.EndpointSwitch, StringComparison.Ordinal))];
}
