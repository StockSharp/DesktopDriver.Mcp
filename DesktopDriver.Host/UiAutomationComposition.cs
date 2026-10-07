namespace StockSharp.DesktopDriver.Host;

using System;
using System.IO;
using System.Runtime.InteropServices;

using StockSharp.DesktopDriver.Protocol;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runtime;
using StockSharp.DesktopDriver.Serialization;
using StockSharp.DesktopDriver.Session;

/// <summary>
/// The part of a driven application that is the same whatever interface toolkit it is built on.
/// </summary>
/// <remarks>
/// A bootstrap creates the composition, builds its toolkit's binder, roots and readers over the registries
/// held here, registers its modules, and then opens the endpoint. The snapshots, the tree, the input policy,
/// the session and the host are decided here once, for every toolkit.
/// </remarks>
public sealed class UiAutomationComposition
{
	/// <summary>
	/// Creates the registries of one run.
	/// </summary>
	public UiAutomationComposition()
	{
		InstanceId = Guid.NewGuid();
		Nodes = new(InstanceId);
	}

	/// <summary>
	/// The run's identifier.
	/// </summary>
	public Guid InstanceId { get; }

	/// <summary>
	/// The nodes the run has handed out.
	/// </summary>
	public UiNodeRegistry Nodes { get; }

	/// <summary>
	/// The adapters the toolkit and the application registered.
	/// </summary>
	public UiAdapterRegistry Adapters { get; } = new();

	/// <summary>
	/// What has changed on each node.
	/// </summary>
	public UiRevisionTracker Revisions { get; } = new();

	/// <summary>
	/// The pictures and files the run keeps for a caller.
	/// </summary>
	public UiArtifactStore Artifacts { get; } = new();

	/// <summary>
	/// Opens the endpoint over what the toolkit supplied.
	/// </summary>
	/// <param name="startup">What the application says about itself.</param>
	/// <param name="toolkit">What the toolkit brings.</param>
	/// <returns>The service and the endpoint it answers on.</returns>
	public UiAutomationEndpoint Open(UiAutomationStartup startup, UiToolkitParts toolkit)
	{
		ArgumentNullException.ThrowIfNull(startup);
		ArgumentNullException.ThrowIfNull(toolkit);

		var journal = new UiActionJournal();
		var snapshots = new UiSnapshotService(InstanceId, Nodes, Adapters, Revisions, toolkit.PresentationReader, toolkit.Executor);

		// Only under a test profile: input goes in through the toolkit's own pipeline, so a product on a live
		// account would be clickable by anything that could reach the endpoint.
		var driver = startup.SafeTestProfile
			? toolkit.CreateInputDriver()
			: new UiNoInputDriver("Input is delivered only to a run started on a test profile.");

		var session = new UiSessionInfo(
			startup.AppId,
			InstanceId,
			startup.AppVersion,
			UiProtocol.Version,
			toolkit.Version,
			RuntimeInformation.FrameworkDescription,
			startup.FixtureId,
			startup.SafeTestProfile,
			driver.BackendId,
			driver.Capabilities,
			UiReadBudget.Default);

		var trees = new UiSemanticTreeBuilder(InstanceId, snapshots, Nodes, Adapters, toolkit.Roots, toolkit.Executor);

		// The search and the registry need each other: a search binds what it finds.
		Nodes.Locator = trees;

		var service = new UiAutomationService(
			session,
			Nodes,
			Adapters,
			toolkit.Roots,
			snapshots,
			Revisions,
			trees,
			new UiWaitService(snapshots, Revisions),
			new UiInputDispatcher(Nodes, Adapters, Revisions, toolkit.InputTargets, driver, toolkit.Executor, journal),
			journal,
			new UiDiagnosticBuffer(),
			Artifacts,
			toolkit.Executor)
		{
			Screenshots = toolkit.Screenshots,
		};

		var host = new UiAutomationHost(service, session);
		var endpoint = host.Start(new UiHostOptions(startup.AppId, startup.FixtureId, startup.SafeTestProfile, UiReadBudget.Default, 10000));

		return new(service, host, endpoint);
	}

	/// <summary>
	/// Writes where the endpoint is to the file a runner named.
	/// </summary>
	/// <param name="endpoint">The endpoint.</param>
	/// <param name="path">The file, or <see langword="null"/> when the runner did not ask for one.</param>
	/// <remarks>
	/// Written whole and then moved into place, so that a runner watching for the file never reads half of one.
	/// </remarks>
	public static void Publish(UiEndpointInfo endpoint, string path)
	{
		ArgumentNullException.ThrowIfNull(endpoint);

		if (string.IsNullOrEmpty(path))
			return;

		var directory = Path.GetDirectoryName(Path.GetFullPath(path));

		if (!string.IsNullOrEmpty(directory))
			Directory.CreateDirectory(directory);

		var temporary = path + ".writing";

		File.WriteAllText(temporary, UiJson.Write(endpoint));
		File.Move(temporary, path, true);
	}
}
