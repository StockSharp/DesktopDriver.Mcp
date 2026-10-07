namespace StockSharp.DesktopDriver.Tests;

using System.Collections.Generic;

using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;

/// <summary>One record the compiled-binding grid shows.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Price">Its price.</param>
public sealed record CompiledBindingRow(string Name, decimal Price);

/// <summary>
/// A grid whose columns are bound the way the products bind theirs.
/// </summary>
public sealed partial class CompiledBindingGrid : UserControl
{
	public CompiledBindingGrid()
	{
		InitializeComponent();

		Grid.ItemsSource = new List<CompiledBindingRow>
		{
			new("First", 10.5m),
			new("Second", 20.25m),
		};
	}

	/// <summary>The grid itself.</summary>
	public DataGrid Grid => Rows;
}
