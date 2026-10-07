namespace StockSharp.DesktopDriver.Cli;

using System;

using StockSharp.DesktopDriver.Runner;

/// <summary>
/// How a command line says which application to reach.
/// </summary>
internal static class UiConnectionArguments
{
	public static UiConnection Read(UiArguments arguments)
	{
		ArgumentNullException.ThrowIfNull(arguments);

		var path = arguments.Optional("endpoint")
			?? Environment.GetEnvironmentVariable(UiConnection.EndpointVariable)
			?? throw new UiUsageException(
				$"Say where the application is with --endpoint, or set {UiConnection.EndpointVariable}.");

		return UiConnection.To(
			path,
			arguments.Optional("app"),
			TimeSpan.FromSeconds(arguments.Number("connect-timeout", 15)));
	}
}
