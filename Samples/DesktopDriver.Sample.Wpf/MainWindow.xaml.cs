namespace DesktopDriver.Sample.Wpf;

using System.Windows;

public partial class MainWindow : Window
{
	public MainWindow() => InitializeComponent();

	private void ApplyClicked(object sender, RoutedEventArgs e)
		=> ResultText.Text = "Applied: " + InputText.Text;
}
