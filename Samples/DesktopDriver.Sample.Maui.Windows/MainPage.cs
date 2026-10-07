namespace DesktopDriver.Sample.Maui.Windows;

using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

public class MainPage : ContentPage
{
	public MainPage()
	{
		Title = "DesktopDriver MAUI Windows";
		BackgroundColor = Colors.White;
		var input = new Entry { AutomationId = "InputText", Text = "Initial text", TextColor = Colors.Black };
		var result = new Label { AutomationId = "ResultText", Text = "Ready", TextColor = Colors.Black };
		var apply = new Button { AutomationId = "ApplyButton", Text = "Apply" };
		apply.Clicked += (_, _) => result.Text = "Applied: " + input.Text;

		Content = new VerticalStackLayout
		{
			Padding = new Thickness(24),
			Spacing = 12,
			Children =
			{
				new Label { Text = "DesktopDriver MAUI Windows", FontSize = 24, TextColor = Colors.Black },
				input,
				apply,
				result,
				new CheckBox { AutomationId = "EnabledToggle", IsChecked = false },
				new CollectionView { AutomationId = "ItemList", ItemsSource = new[] { "Alpha", "Beta", "Gamma" }, HeightRequest = 120 },
			},
		};
	}
}
