namespace StockSharp.DesktopDriver.Tests;

using System;

using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Threading;

using StockSharp.DesktopDriver.Adapters;
using StockSharp.DesktopDriver.Avalonia;
using StockSharp.DesktopDriver.Avalonia.Docking;
using StockSharp.DesktopDriver.Avalonia.Grids;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Runtime;


/// <summary>
/// A real StockSharp control in a real window, with the module around it.
/// </summary>
/// <remarks>
/// The control is the shipping one, filled through its own public API: one built for the test would
/// answer questions about itself rather than about what the product shows.
/// </remarks>
internal sealed class ControlHarness : IDisposable
{
	private readonly IDisposable _standard;
	private readonly IDisposable _grids;
	private readonly IDisposable _docking;

	public ControlHarness(Control control, double width = 900, double height = 400)
	{
		ArgumentNullException.ThrowIfNull(control);

		Control = control;
		InstanceId = Guid.NewGuid();
		Nodes = new UiNodeRegistry(InstanceId);
		Adapters = new UiAdapterRegistry();
		Revisions = new UiRevisionTracker();
		Executor = new AvaloniaUiExecutor();
		Binder = new AvaloniaNodeBinder(Nodes, Revisions);

		_standard = new AvaloniaAutomationModule(Binder).Register(Adapters);
		_grids = new DataGridAutomationModule(Binder).Register(Adapters);
		_docking = new DockAutomationModule(Binder).Register(Adapters);

		Window = new Window { Name = "GridWindow", Width = width, Height = height, Content = control };
		Window.Show();

		// A grid builds its columns when its template is applied and its rows on the layout pass after
		// that, so the window is let finish twice. A reader asked in between would honestly report a grid
		// with no columns, which is true and useless.
		Settle();
		Settle();

		Roots = new AvaloniaRootTracker(Binder) { Explicit = [Window] };

		Snapshots = new UiSnapshotService(
			InstanceId, Nodes, Adapters, Revisions, new AvaloniaPresentationReader(), Executor);

		Tree = new UiSemanticTreeBuilder(InstanceId, Snapshots, Nodes, Adapters, Roots, Executor);
		Nodes.Locator = Tree;
	}

	public Guid InstanceId { get; }

	public Control Control { get; }

	public Window Window { get; }

	public UiNodeRegistry Nodes { get; }

	public UiAdapterRegistry Adapters { get; }

	public UiRevisionTracker Revisions { get; }

	public AvaloniaNodeBinder Binder { get; }

	public AvaloniaRootTracker Roots { get; }

	public AvaloniaUiExecutor Executor { get; }

	public UiSnapshotService Snapshots { get; }

	public UiSemanticTreeBuilder Tree { get; }

	/// <summary>
	/// The control's address.
	/// </summary>
	public UiNodeId Id => Binder.Bind(Control).Id;

	/// <summary>
	/// The grid as the adapters see it.
	/// </summary>
	public UiSubject Subject => Nodes.Resolve(UiTarget.FromId(Id));

	/// <summary>
	/// The adapter that answers for the control.
	/// </summary>
	public IUiSnapshotAdapter Adapter => Adapters.Resolve(Subject);

	/// <summary>
	/// A control inside this one, as the adapters see it.
	/// </summary>
	/// <param name="control">The control.</param>
	/// <returns>What the adapters answer for.</returns>
	public UiSubject SubjectOf(Control control)
		=> Nodes.Resolve(UiTarget.FromId(Binder.Bind(control).Id));

	/// <summary>
	/// What an adapter is told about a read of a control inside this one.
	/// </summary>
	/// <param name="control">The control.</param>
	/// <returns>The context.</returns>
	public UiCaptureContext ContextOf(Control control)
	{
		var id = Binder.Bind(control).Id;

		return new(Nodes.GetReference(id), Revisions.Read(id), Queries.UiCaptureOptions.Default);
	}

	/// <summary>
	/// The same adapter, when the control is a table.
	/// </summary>
	public IUiGridAdapter GridAdapter => (IUiGridAdapter)Adapter;

	/// <summary>
	/// The same adapter, when the control is a chart.
	/// </summary>
	public IUiChartAdapter ChartAdapter => (IUiChartAdapter)Adapter;

	/// <summary>
	/// What an adapter is told about a read.
	/// </summary>
	public UiCaptureContext Context => new(
		Nodes.GetReference(Id),
		Revisions.Read(Id),
		Queries.UiCaptureOptions.Default);

	/// <summary>
	/// Lets the interface finish what the last change started.
	/// </summary>
	public void Settle()
	{
		Dispatcher.UIThread.RunJobs();
		Window.UpdateLayout();

		// Drawing a frame is what makes a headless window behave like one somebody is looking at: until
		// something renders, a control's template is never applied, and a grid without its template has
		// not built its columns yet.
		Window.CaptureRenderedFrame()?.Dispose();

		Dispatcher.UIThread.RunJobs();
	}

	public void Dispose()
	{
		_docking.Dispose();
		_grids.Dispose();
		_standard.Dispose();
		Window.Close();
	}
}
