namespace DesktopDriver.Sample.Wpf;

using System.Windows;

using StockSharp.DesktopDriver.Bootstrap.Wpf;
using StockSharp.DesktopDriver.Host;

public partial class App : Application
{
	private WpfAutomationRuntime _automation;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		MainWindow = new MainWindow();
		MainWindow.Show();

		if (UiAutomationLaunch.TryRead(e.Args) is { } launch)
		{
			_automation = WpfAutomationBootstrap.Start(
				UiAutomationStartup.For("desktopdriver.sample.wpf", typeof(App).Assembly.GetName().Version?.ToString(),
					fixtureId: null, hasLiveOutsideWorld: false, isTestProfile: false),
				launch.EndpointFile, []);
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_automation?.DisposeAsync().AsTask().GetAwaiter().GetResult();
		base.OnExit(e);
	}
}
