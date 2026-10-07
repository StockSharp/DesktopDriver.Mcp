namespace StockSharp.DesktopDriver.Tests.Protocol;

using System;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Threading;

using StockSharp.DesktopDriver.Avalonia;
using StockSharp.DesktopDriver.Avalonia.Headless;
using StockSharp.DesktopDriver.Client;
using StockSharp.DesktopDriver.Host;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// A real window, the module around it, and an endpoint open on it - the composition an application
/// started for automation ends up with.
/// </summary>
/// <remarks>
/// Everything here happens on the interface's own thread, including the calls that travel over the
/// channel: those wait asynchronously, so the thread stays free to answer them. A caller that held
/// that thread while waiting for an answer from it would be waiting for itself.
/// </remarks>
internal sealed class HostedApplication : IAsyncDisposable
{
	/// <summary>
	/// The product these tests pretend to be.
	/// </summary>
	public const string AppId = "tests.protocol";

	private readonly IDisposable _module;

	private HostedApplication(UiReadBudget budget)
	{
		var instanceId = Guid.NewGuid();
		var nodes = new UiNodeRegistry(instanceId);
		var adapters = new UiAdapterRegistry();
		var revisions = new UiRevisionTracker();
		var executor = new AvaloniaUiExecutor();
		var binder = new AvaloniaNodeBinder(nodes, revisions);

		_module = new AvaloniaAutomationModule(binder).Register(adapters);

		var button = new Button { Name = "TheButton", Content = "Press me", Width = 120, Height = 40 };

		// Something a paged read can answer for. The button is read as a control; nothing about it goes
		// through the read pipeline the pages come out of.
		var tree = new TreeView
		{
			Name = "TheTree",
			ItemsSource = new[] { "first", "second", "third" },
		};

		Window = new Window
		{
			Name = "TestWindow",
			Width = 400,
			Height = 300,
			Content = new StackPanel { Children = { button, tree } },
		};
		Window.Show();
		Dispatcher.UIThread.RunJobs();
		Window.UpdateLayout();

		// A window is hit-tested through the frame composed of it, and an application opens its endpoint only
		// once one has been; the hosted one is not let answer earlier either.
		AvaloniaHeadlessPlatform.ForceRenderTimerTick();
		Dispatcher.UIThread.RunJobs();

		var roots = new AvaloniaRootTracker(binder) { Explicit = [Window] };

		var snapshots = new UiSnapshotService(
			instanceId, nodes, adapters, revisions, new AvaloniaPresentationReader(), executor);

		var trees = new UiSemanticTreeBuilder(instanceId, snapshots, nodes, adapters, roots, executor);

		nodes.Locator = trees;

		var journal = new UiActionJournal();

		Session = new UiSessionInfo(
			AppId,
			instanceId,
			"1.0.0",
			"1.0",
			"12.1.2",
			Environment.Version.ToString(),
			"default",
			true,
			"avalonia.headless",
			["input.click", "input.text"],
			budget);

		Service = new UiAutomationService(
			Session,
			nodes,
			adapters,
			roots,
			snapshots,
			revisions,
			trees,
			new UiWaitService(snapshots, revisions),
			new UiInputDispatcher(
				nodes,
				adapters,
				revisions,
				new AvaloniaInputTargetResolver(),
				new HeadlessInputDriver(nodes, executor),
				executor,
				journal),
			journal,
			new UiDiagnosticBuffer(),
			new UiArtifactStore(),
			executor);

		Host = new UiAutomationHost(Service, Session);
		ButtonId = binder.Bind(button).Id;
		TreeId = binder.Bind(tree).Id;

		button.Click += (_, _) => Clicks++;
	}

	/// <summary>
	/// The window the application shows.
	/// </summary>
	public Window Window { get; }

	/// <summary>
	/// What it says about itself.
	/// </summary>
	public UiSessionInfo Session { get; }

	/// <summary>
	/// The one implementation, as a caller inside the process sees it.
	/// </summary>
	public UiAutomationService Service { get; }

	/// <summary>
	/// The endpoint a caller in another process reaches it through.
	/// </summary>
	public UiAutomationHost Host { get; }

	/// <summary>
	/// The button both sides read and press.
	/// </summary>
	public UiNodeId ButtonId { get; }

	/// <summary>
	/// The tree a paged read is answered from.
	/// </summary>
	public UiNodeId TreeId { get; }

	/// <summary>
	/// How many times the button actually ran what it does.
	/// </summary>
	public int Clicks { get; private set; }

	/// <summary>
	/// Starts an application and opens its endpoint.
	/// </summary>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The running application.</returns>
	public static Task<HostedApplication> StartAsync(CancellationToken cancellationToken)
		=> StartAsync(UiReadBudget.Default, cancellationToken);

	/// <summary>
	/// Starts an application that is allowed to answer with only so much.
	/// </summary>
	/// <param name="budget">How much one reply may carry.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The running application.</returns>
	public static async Task<HostedApplication> StartAsync(UiReadBudget budget, CancellationToken cancellationToken)
	{
		var hosted = new HostedApplication(budget);

		await hosted.Host.StartAsync(UiHostOptions.ForApp(AppId), cancellationToken);

		return hosted;
	}

	/// <summary>
	/// Connects to it the way a runner does.
	/// </summary>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The connected client.</returns>
	public Task<UiAutomationClient> ConnectAsync(CancellationToken cancellationToken)
		=> ConnectAsync(AppId, cancellationToken);

	/// <summary>
	/// Connects with the product the caller expects, to see what the endpoint makes of it.
	/// </summary>
	/// <param name="expectedAppId">The product the caller believes it reached.</param>
	/// <param name="cancellationToken">Cancellation.</param>
	/// <returns>The connected client.</returns>
	public Task<UiAutomationClient> ConnectAsync(string expectedAppId, CancellationToken cancellationToken)
		=> UiAutomationClient.ConnectAsync(
			Host.Endpoint,
			expectedAppId,
			TimeSpan.FromSeconds(10),
			cancellationToken);

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await Host.DisposeAsync();

		_module.Dispose();
		Window.Close();
	}
}
