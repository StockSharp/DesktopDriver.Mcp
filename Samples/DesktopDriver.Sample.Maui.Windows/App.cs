namespace DesktopDriver.Sample.Maui.Windows;

using System;

using Microsoft.Maui;
using Microsoft.Maui.Controls;

using StockSharp.DesktopDriver.Bootstrap.Maui;
using StockSharp.DesktopDriver.Host;

public class App : Application
{
	protected override Window CreateWindow(IActivationState activationState)
	{
		var window = new Window(new MainPage())
		{
			AutomationId = "MainWindow",
			Title = "DesktopDriver MAUI Windows sample",
			Width = 640,
			Height = 480,
		};

		if (UiAutomationLaunch.TryRead(Environment.GetCommandLineArgs()) is { } launch)
		{
			MauiAutomationRuntime automation = null;

			// Created runs on the Windows dispatcher after the window's native handler exists.
			window.Created += (_, _) => automation = MauiAutomationBootstrap.Start(
				UiAutomationStartup.For("desktopdriver.sample.maui.windows", typeof(App).Assembly.GetName().Version?.ToString(),
					fixtureId: null, hasLiveOutsideWorld: false, isTestProfile: false),
				launch.EndpointFile, []);
			window.Destroying += (_, _) => automation?.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}

		return window;
	}
}
