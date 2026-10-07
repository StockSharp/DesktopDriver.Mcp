namespace StockSharp.DesktopDriver.Bootstrap.Avalonia;

using System;
using System.Collections.Generic;
using System.Linq;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Threading;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Avalonia;
using StockSharp.DesktopDriver.Host;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// Turns an Avalonia application into one that can be driven, when it is started that way.
/// </summary>
/// <remarks>
/// One line in an application's startup, and nothing at all when the switch is absent: no endpoint is
/// opened and no adapters are registered, so an ordinary copy of the product has
/// nothing extra listening. This is the whole of the integration - an application does not implement
/// anything of its own to be driven.
/// </remarks>
public static class AvaloniaAutomationBootstrap
{
	/// <summary>
	/// Composes the module around a running application and opens its endpoint.
	/// </summary>
	/// <param name="startup">What the application says about itself.</param>
	/// <param name="endpointFile">Where to write the endpoint once it is open, if anywhere.</param>
	/// <param name="extraModules">
	/// Adapters for controls beyond Avalonia's own - a grid, a docking workspace, the application's
	/// controls - each made for the binder this call creates.
	/// </param>
	/// <returns>What was started.</returns>
	/// <remarks>
	/// The modules come from the application, so it carries the assemblies of the controls it uses and no
	/// others.
	/// </remarks>
	public static AvaloniaAutomationRuntime Start(
		UiAutomationStartup startup,
		string endpointFile,
		IReadOnlyList<Func<AvaloniaNodeBinder, IUiAutomationModule>> extraModules)
	{
		ArgumentNullException.ThrowIfNull(startup);

		var composition = new UiAutomationComposition();
		var executor = new AvaloniaUiExecutor();
		var binder = new AvaloniaNodeBinder(composition.Nodes, composition.Revisions);

		// Avalonia's own controls are always read; what else the application is built from it says.
		var registration = new UiCompositeRegistration(
		[
			new AvaloniaAutomationModule(binder).Register(composition.Adapters),
			.. (extraModules ?? []).Select(module => module(binder).Register(composition.Adapters)),
		]);

		// A visual that has left every window is not what its address means any more: a page rebuilt under
		// the same address is searched for again rather than answered with the one that is gone.
		composition.Nodes.IsLive = instance => instance is not Visual visual || TopLevel.GetTopLevel(visual) is not null;

		var opened = composition.Open(startup, new UiToolkitParts(
			executor,
			new AvaloniaRootTracker(binder),
			new AvaloniaPresentationReader(),
			new AvaloniaInputTargetResolver(),
			() => new AvaloniaSyntheticInputDriver(composition.Nodes, executor),
			new AvaloniaScreenshotService(
				composition.Nodes, composition.Adapters, composition.Revisions, executor, composition.Artifacts, composition.InstanceId).CaptureAsync,
			typeof(Application).Assembly.GetName().Version?.ToString()));

		PublishWhenSettled(opened.Endpoint, endpointFile);

		return new AvaloniaAutomationRuntime(opened.Service, opened.Host, opened.Endpoint, binder, registration);
	}

	private static void PublishWhenSettled(UiEndpointInfo endpoint, string path)
	{
		if (string.IsNullOrEmpty(path))
			return;

		// The file is what tells a runner it may connect, so it is written once the interface has been
		// laid out and drawn rather than while it is still being built. Written at start-up instead, the
		// first thing a runner did would be to ask about controls that have no position on screen yet and
		// cannot be clicked. Posted below the priorities layout and rendering run at, and never waited on:
		// blocking here would be blocking the very work it is waiting for.
		Dispatcher.UIThread.Post(() => UiAutomationComposition.Publish(endpoint, path), DispatcherPriority.Background);
	}
}
