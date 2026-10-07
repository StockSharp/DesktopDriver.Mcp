namespace StockSharp.DesktopDriver.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;

using global::Avalonia.Controls;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Avalonia;
using StockSharp.DesktopDriver.Avalonia.Headless;
using StockSharp.DesktopDriver.Runtime;

/// <summary>
/// One application's worth of the module, wired the way a real host wires it.
/// </summary>
/// <remarks>
/// Deliberately the same composition a product uses: registry, adapter registry, revisions, the
/// standard Avalonia module, the input resolver and a driver. A fixture that assembled something
/// simpler would be testing something other than what ships.
/// </remarks>
internal sealed class AutomationFixture : IDisposable
{
	private readonly IDisposable _module;

	public AutomationFixture(Window window)
		: this(window, (nodes, executor) => new HeadlessInputDriver(nodes, executor))
	{
	}

	private AutomationFixture(Window window, Func<UiNodeRegistry, AvaloniaUiExecutor, IUiInputDriver> driver)
	{
		ArgumentNullException.ThrowIfNull(window);
		ArgumentNullException.ThrowIfNull(driver);

		Window = window;
		InstanceId = Guid.NewGuid();
		Nodes = new UiNodeRegistry(InstanceId);
		Adapters = new UiAdapterRegistry();
		Revisions = new UiRevisionTracker();
		Executor = new AvaloniaUiExecutor();
		Binder = new AvaloniaNodeBinder(Nodes, Revisions);
		Roots = new AvaloniaRootTracker(Binder) { Explicit = [window] };

		_module = new AvaloniaAutomationModule(Binder).Register(Adapters);

		Snapshots = new UiSnapshotService(
			InstanceId, Nodes, Adapters, Revisions, new AvaloniaPresentationReader(), Executor);

		Tree = new UiSemanticTreeBuilder(InstanceId, Snapshots, Nodes, Adapters, Roots, Executor);

		// The same wiring a product uses: a caller naming a control it has never read still finds it.
		Nodes.Locator = Tree;
		Waits = new UiWaitService(Snapshots, Revisions);
		Journal = new UiActionJournal();

		Input = new UiInputDispatcher(
			Nodes,
			Adapters,
			Revisions,
			new AvaloniaInputTargetResolver(),
			driver(Nodes, Executor),
			Executor,
			Journal);
	}

	/// <summary>
	/// The module as a running product has it: input sent into the interface itself rather than through a
	/// headless platform.
	/// </summary>
	public static AutomationFixture Synthetic(Window window)
		=> new(window, (nodes, executor) => new AvaloniaSyntheticInputDriver(nodes, executor));

	public Guid InstanceId { get; }

	public Window Window { get; }

	public UiNodeRegistry Nodes { get; }

	public UiAdapterRegistry Adapters { get; }

	public UiRevisionTracker Revisions { get; }

	public AvaloniaNodeBinder Binder { get; }

	public AvaloniaRootTracker Roots { get; }

	public AvaloniaUiExecutor Executor { get; }

	public UiSnapshotService Snapshots { get; }

	public UiSemanticTreeBuilder Tree { get; }

	public UiWaitService Waits { get; }

	public UiActionJournal Journal { get; }

	public UiInputDispatcher Input { get; }

	/// <summary>
	/// Registers a control and returns the address to address it by.
	/// </summary>
	public Identity.UiNodeId Bind(Control control) => Binder.Bind(control)?.Id;

	public void Dispose()
	{
		_module.Dispose();
		Window.Close();
	}
}
