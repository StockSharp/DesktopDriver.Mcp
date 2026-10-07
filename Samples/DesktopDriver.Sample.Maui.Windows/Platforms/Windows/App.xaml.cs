namespace DesktopDriver.Sample.Maui.Windows.WinUI;

using Microsoft.Maui;
using Microsoft.Maui.Hosting;

public partial class App : MauiWinUIApplication
{
	public App() => InitializeComponent();

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
