namespace StockSharp.DesktopDriver.Bootstrap.Maui;

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Maui.Controls;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Host;
using StockSharp.DesktopDriver.Maui;

/// <summary>
/// Starts automation inside a MAUI application.
/// </summary>
public static class MauiAutomationBootstrap
{
	/// <summary>
	/// Starts listening for runners.
	/// </summary>
	/// <param name="startup">What the application says about itself.</param>
	/// <param name="endpointFile">Where to write how to reach it, or <see langword="null"/> for nowhere.</param>
	/// <param name="extraModules">Adapters of the product's own controls.</param>
	/// <returns>The running automation.</returns>
	public static MauiAutomationRuntime Start(
		UiAutomationStartup startup,
		string endpointFile,
		IReadOnlyList<Func<MauiNodeBinder, IUiAutomationModule>> extraModules)
	{
		ArgumentNullException.ThrowIfNull(startup);

		var composition = new UiAutomationComposition();
		var executor = new MauiUiExecutor();
		var binder = new MauiNodeBinder(composition.Nodes, composition.Revisions);

		var registration = new UiCompositeRegistration(
		[
			new MauiAutomationModule(binder).Register(composition.Adapters),
			.. (extraModules ?? []).Select(module => module(binder).Register(composition.Adapters)),
		]);

		var opened = composition.Open(startup, new UiToolkitParts(
			executor,
			new MauiRootTracker(binder),
			new MauiPresentationReader(),
			new MauiInputTargetResolver(),
			() => new MauiInputDriver(composition.Nodes, executor),
			new MauiScreenshotService(
				composition.Nodes, composition.Adapters, composition.Revisions, executor, composition.Artifacts, composition.InstanceId).CaptureAsync,
			typeof(Application).Assembly.GetName().Version?.ToString()));

		// Written as soon as the endpoint is listening, which is the only thing the file says: what a runner may
		// do once connected is its own question, and it waits for the window it wants before touching anything.
		UiAutomationComposition.Publish(opened.Endpoint, endpointFile);

		return new MauiAutomationRuntime(opened.Service, opened.Host, opened.Endpoint, binder, registration);
	}
}
