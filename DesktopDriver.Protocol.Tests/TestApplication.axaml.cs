namespace StockSharp.DesktopDriver.Tests.Protocol;

using global::Avalonia;
using global::Avalonia.Markup.Xaml;

/// <summary>
/// The application the headless session runs.
/// </summary>
public sealed class TestApplication : Application
{
	/// <inheritdoc />
	public override void Initialize() => AvaloniaXamlLoader.Load(this);
}
