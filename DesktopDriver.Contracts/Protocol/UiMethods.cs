namespace StockSharp.DesktopDriver.Protocol;

/// <summary>
/// The operations the protocol has, by the names they travel under.
/// </summary>
/// <remarks>
/// A closed list. A method name arriving on the wire is looked up here and nowhere else: a host that
/// turned a string into a call by reflection would let whoever can reach the pipe run anything the
/// process can.
/// </remarks>
public static class UiMethods
{
	/// <summary>Opens a session. The first message of every connection.</summary>
	public const string SessionOpen = "session.open";

	/// <summary>Who answered and what it can do.</summary>
	public const string SessionInfo = "session.info";

	/// <summary>The windows and popups on screen.</summary>
	public const string Windows = "windows";

	/// <summary>A bounded search for nodes.</summary>
	public const string Find = "find";

	/// <summary>A bounded walk of the tree.</summary>
	public const string Tree = "tree";

	/// <summary>One node.</summary>
	public const string Snapshot = "snapshot";

	/// <summary>The columns of a grid.</summary>
	public const string GridColumns = "grid.columns";

	/// <summary>The rows of a grid.</summary>
	public const string GridRows = "grid.rows";

	/// <summary>The groups of a grid.</summary>
	public const string GridGroups = "grid.groups";

	/// <summary>The series of a chart.</summary>
	public const string ChartSeries = "chart.series";

	/// <summary>The points of one series.</summary>
	public const string ChartPoints = "chart.points";

	/// <summary>The price levels of an order book.</summary>
	public const string OrderBookLevels = "orderBook.levels";

	/// <summary>The properties a property editor is showing.</summary>
	public const string PropertyEditorItems = "propertyEditor.items";

	/// <summary>The items a tree is showing.</summary>
	public const string TreeItems = "tree.items";

	/// <summary>The lines of a document.</summary>
	public const string DocumentContent = "document.content";

	/// <summary>The blocks of a diagram.</summary>
	public const string DiagramNodes = "diagram.nodes";

	/// <summary>The connections between them.</summary>
	public const string DiagramConnections = "diagram.connections";

	/// <summary>A docking layout.</summary>
	public const string DockLayout = "dock.layout";

	/// <summary>Do something to the interface.</summary>
	public const string Input = "input";

	/// <summary>What became of an action.</summary>
	public const string ActionStatus = "action.status";

	/// <summary>Wait for a condition.</summary>
	public const string Wait = "wait";

	/// <summary>Take a picture.</summary>
	public const string Screenshot = "screenshot";

	/// <summary>Read part of an artifact.</summary>
	public const string ArtifactRead = "artifact.read";

	/// <summary>Read what the module noticed.</summary>
	public const string Diagnostics = "diagnostics";

	/// <summary>Abandon a request that is still running.</summary>
	public const string RequestCancel = "request.cancel";
}
