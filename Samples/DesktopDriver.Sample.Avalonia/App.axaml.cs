namespace DesktopDriver.Sample;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using StockSharp.DesktopDriver.Avalonia.Grids;
using StockSharp.DesktopDriver.Bootstrap.Avalonia;
using StockSharp.DesktopDriver.Host;

/// <summary>
/// The sample application.
/// </summary>
public partial class App : Application
{
	/// <summary>
	/// The identifier a runner names this application by.
	/// </summary>
	public const string AppId = "desktopdriver.sample";

	private static AvaloniaAutomationRuntime _automation;

	/// <inheritdoc />
	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	/// <inheritdoc />
	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = new MainWindow();

			// After the window exists, so that a runner connecting at once finds something to read. Nothing
			// here reaches outside the process, so the run is safe to click through without a test profile.
			if (UiAutomationLaunch.TryRead(desktop.Args) is { } launch)
			{
				_automation = AvaloniaAutomationBootstrap.Start(
					UiAutomationStartup.For(AppId, typeof(App).Assembly.GetName().Version?.ToString(), null, false, false),
					launch.EndpointFile,
					[binder => new DataGridAutomationModule(binder)]);
			}
		}

		base.OnFrameworkInitializationCompleted();
	}

	/// <summary>
	/// Closes the automation endpoint, if one was opened.
	/// </summary>
	public static void StopAutomation()
	{
		var automation = _automation;

		_automation = null;

		automation?.DisposeAsync().AsTask().GetAwaiter().GetResult();
	}
}
