namespace StockSharp.DesktopDriver.Bootstrap.Wpf;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Host;
using StockSharp.DesktopDriver.Wpf;

/// <summary>
/// Turns a WPF application into one that can be driven, when it is started that way.
/// </summary>
/// <remarks>
/// One line in an application's startup, and nothing at all when the switch is absent: no endpoint is
/// opened and no adapters are registered, so an ordinary copy of the product has
/// nothing extra listening. This is the whole of the integration - an application does not implement
/// anything of its own to be driven.
/// </remarks>
public static class WpfAutomationBootstrap
{
	/// <summary>
	/// Composes the module around a running application and opens its endpoint.
	/// </summary>
	/// <param name="startup">What the application says about itself.</param>
	/// <param name="endpointFile">Where to write the endpoint once it is open, if anywhere.</param>
	/// <param name="extraModules">
	/// Adapters for controls beyond WPF's own - a control library, the application's controls - each made
	/// for the binder this call creates.
	/// </param>
	/// <returns>What was started.</returns>
	public static WpfAutomationRuntime Start(
		UiAutomationStartup startup,
		string endpointFile,
		IReadOnlyList<Func<WpfNodeBinder, IUiAutomationModule>> extraModules)
	{
		ArgumentNullException.ThrowIfNull(startup);

		var composition = new UiAutomationComposition();
		var executor = new WpfUiExecutor();
		var binder = new WpfNodeBinder(composition.Nodes, composition.Revisions);

		// WPF's own controls are always read; what else the application is built from it says.
		var registration = new UiCompositeRegistration(
		[
			new WpfAutomationModule(binder).Register(composition.Adapters),
			.. (extraModules ?? []).Select(module => module(binder).Register(composition.Adapters)),
		]);

		var opened = composition.Open(startup, new UiToolkitParts(
			executor,
			new WpfRootTracker(binder),
			new WpfPresentationReader(),
			new WpfInputTargetResolver(),
			() => new WpfSyntheticInputDriver(composition.Nodes, executor),
			new WpfScreenshotService(
				composition.Nodes, composition.Adapters, composition.Revisions, executor, composition.Artifacts, composition.InstanceId).CaptureAsync,
			typeof(Application).Assembly.GetName().Version?.ToString()));

		// Written as soon as the endpoint is listening, which is the only thing the file says. Waiting for
		// the interface to settle first sounds safer and is not: it was posted below the priorities layout
		// runs at, and a product that keeps its own thread busy while it starts - one that compiles, or
		// loads a workspace - never reaches that priority, so the file never appeared and the product was
		// unreachable for as long as it was busy. What a runner may do once connected is its own question,
		// and it already waits for the window it wants before touching anything.
		UiAutomationComposition.Publish(opened.Endpoint, endpointFile);

		return new WpfAutomationRuntime(opened.Service, opened.Host, opened.Endpoint, binder, registration);
	}
}
