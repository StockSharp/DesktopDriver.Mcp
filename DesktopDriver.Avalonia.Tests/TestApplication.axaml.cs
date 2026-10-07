namespace StockSharp.DesktopDriver.Tests;

using global::Avalonia;
using global::Avalonia.Headless;
using global::Avalonia.Markup.Xaml;

/// <summary>
/// The application the headless session runs.
/// </summary>
public sealed class TestApplication : Application
{
	/// <inheritdoc />
	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	/// <summary>
	/// Builds the session's application.
	/// </summary>
	/// <returns>The builder the headless session starts from.</returns>
	/// <remarks>
	/// Headless normally substitutes a renderer that measures and lays out but paints nothing, and a
	/// picture taken under it comes back blank whatever the control does. These tests read the pixels, so
	/// they ask for the real one.
	/// </remarks>
	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder
			.Configure<TestApplication>()
			.UseSkia()
			.UseHeadless(new() { UseHeadlessDrawing = false });
}
