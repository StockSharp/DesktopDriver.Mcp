namespace DesktopDriver.Sample;

using System;

using Avalonia;

internal static class Program
{
	[STAThread]
	public static int Main(string[] args)
	{
		try
		{
			return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
		}
		finally
		{
			App.StopAutomation();
		}
	}

	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.LogToTrace();
}
