namespace DesktopDriver.Sample;

using System.Collections.ObjectModel;

using Avalonia.Controls;
using Avalonia.Interactivity;

/// <summary>
/// The window the sample shows: a table of instruments, the markets they trade on, and a way to add one.
/// </summary>
public partial class MainWindow : Window
{
	private readonly ObservableCollection<Instrument> _instruments =
	[
		new("AAPL", "NASDAQ", 227.52m, 51_200),
		new("MSFT", "NASDAQ", 431.10m, 20_450),
		new("NVDA", "NASDAQ", 118.85m, 312_000),
		new("AMZN", "NASDAQ", 186.40m, 40_110),
		new("GOOG", "NASDAQ", 165.20m, 22_870),
		new("IBM", "NYSE", 214.33m, 4_020),
		new("KO", "NYSE", 70.12m, 11_340),
		new("JPM", "NYSE", 211.05m, 9_870),
		new("XOM", "NYSE", 117.64m, 15_300),
		new("ES", "CME", 5_721.25m, 1_250_000),
		new("NQ", "CME", 20_110.50m, 520_000),
		new("CL", "CME", 68.37m, 310_000),
	];

	/// <summary>
	/// Creates the window.
	/// </summary>
	public MainWindow()
	{
		InitializeComponent();

		Instruments.ItemsSource = _instruments;
	}

	private void OnAdd(object sender, RoutedEventArgs e)
	{
		var symbol = SymbolBox.Text?.Trim();

		if (string.IsNullOrEmpty(symbol))
			return;

		_instruments.Add(new(symbol.ToUpperInvariant(), "NASDAQ", 0m, 0));
		SymbolBox.Text = string.Empty;
	}
}
