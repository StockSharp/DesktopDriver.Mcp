namespace StockSharp.DesktopDriver.Cli;

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using StockSharp.DesktopDriver.Client;
using StockSharp.DesktopDriver.Diagnostics;
using StockSharp.DesktopDriver.Identity;
using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Runner;
using StockSharp.DesktopDriver.Serialization;
using StockSharp.DesktopDriver.States;
using StockSharp.DesktopDriver.Values;
using StockSharp.DesktopDriver.Waiting;

/// <summary>
/// What this program can be asked to do.
/// </summary>
/// <remarks>
/// One branch per operation the protocol has, and nothing else. A program that turned the word it was
/// given into a call by reflection would run whatever the protocol grew next, named by whoever wrote the
/// script, before anybody had decided that it should be reachable from a command line.
/// <para>
/// Each branch reads its own options before anything is connected, so a misspelt option is refused in
/// the moment rather than after a window has been clicked.
/// </para>
/// </remarks>
internal static class UiCommands
{
	internal delegate Task<string> Operation(UiAutomationClient client, CancellationToken cancellationToken);

	public static async Task<string> RunAsync(
		string name,
		UiArguments arguments,
		CancellationToken cancellationToken)
	{
		var operation = Plan(name, arguments);
		var connection = UiConnectionArguments.Read(arguments);

		arguments.RefuseUnknown();

		await using var client = await connection.OpenAsync(cancellationToken);

		return await operation(client, cancellationToken);
	}

	internal static Operation Plan(string name, UiArguments arguments)
		=> name switch
		{
			"session" => Session(),
			"windows" => Windows(),
			"find" => Find(arguments),
			"tree" => Tree(arguments),
			"snapshot" => Snapshot(arguments),
			"grid-columns" => GridColumns(arguments),
			"grid-rows" => GridRows(arguments),
			"grid-groups" => GridGroups(arguments),
			"chart-series" => ChartSeries(arguments),
			"chart-points" => ChartPoints(arguments),
			"book-levels" => BookLevels(arguments),
			"property-items" => PropertyItems(arguments),
			"tree-items" => TreeItems(arguments),
			"document-content" => DocumentContent(arguments),
			"diagram-nodes" => DiagramNodes(arguments),
			"diagram-connections" => DiagramConnections(arguments),
			"dock-layout" => DockLayout(arguments),
			"click" => Click(arguments),
			"type" => Type(arguments),
			"key" => Key(arguments),
			"scroll" => Scroll(arguments),
			"show" => Input(arguments, UiEnsureVisibleAction.Instance),
			"action-status" => ActionStatus(arguments),
			"wait" => Wait(arguments),
			"screenshot" => Screenshot(arguments),
			"artifact" => Artifact(arguments),
			"diagnostics" => Diagnostics(arguments),
			_ => throw new UiUsageException($"There is no operation called '{name}'."),
		};

	private static Operation Session()
		=> async (client, token) => UiJson.Write(await client.GetSessionAsync(token));

	private static Operation Windows()
		=> async (client, token) => UiJson.Write(await client.GetSurfacesAsync(token));

	private static Operation Find(UiArguments arguments)
	{
		var selector = new UiSelector(
			arguments.Optional("scope"),
			arguments.Optional("automation-id"),
			arguments.Optional("name"),
			arguments.Optional("kind"),
			arguments.Optional("text"),
			arguments.Optional("surface"));

		if (selector.IsEmpty)
		{
			throw new UiUsageException(
				"find needs something to look for: --scope, --automation-id, --name, --kind, --text or --surface.");
		}

		var query = new UiFindQuery(selector, Page(arguments));

		return async (client, token) => UiJson.Write(await client.FindAsync(query, token));
	}

	private static Operation Tree(UiArguments arguments)
	{
		// No node means the whole application: a caller that has not found anything yet has nothing to name.
		var query = new UiTreeQuery(
			OptionalNode(arguments),
			arguments.Flag("visual-internals"),
			Capture(arguments));

		return async (client, token) => UiJson.Write(await client.GetTreeAsync(query, token));
	}

	private static Operation Snapshot(UiArguments arguments)
	{
		var target = Node(arguments);
		var options = Capture(arguments);

		return async (client, token) => UiJson.Write(await client.CaptureAsync(target, options, token));
	}

	private static Operation GridColumns(UiArguments arguments)
	{
		var target = Node(arguments);
		var query = new GridColumnsQuery(arguments.List("columns"), Page(arguments), null);

		return async (client, token) => UiJson.Write(await client.ReadGridColumnsAsync(target, query, token));
	}

	private static Operation GridRows(UiArguments arguments)
	{
		var target = Node(arguments);
		var query = new GridRowsQuery(
			arguments.Choice("mode", GridRowsModes.ViewData),
			arguments.Index("start"),
			arguments.List("rows"),
			arguments.List("columns"),
			Page(arguments),
			null);

		return async (client, token) => UiJson.Write(await client.ReadGridRowsAsync(target, query, token));
	}

	private static Operation GridGroups(UiArguments arguments)
	{
		var target = Node(arguments);
		var query = new GridGroupsQuery(arguments.Optional("parent"), Page(arguments), null);

		return async (client, token) => UiJson.Write(await client.ReadGridGroupsAsync(target, query, token));
	}

	private static Operation ChartSeries(UiArguments arguments)
	{
		var target = Node(arguments);
		var query = new ChartSeriesQuery(Page(arguments), null);

		return async (client, token) => UiJson.Write(await client.ReadChartSeriesAsync(target, query, token));
	}

	private static Operation ChartPoints(UiArguments arguments)
	{
		var target = Node(arguments);
		var query = new ChartPointsQuery(
			arguments.Required("series"),
			arguments.Index("start"),
			arguments.Moment("from"),
			arguments.Moment("to"),
			arguments.List("keys"),
			Page(arguments),
			null);

		return async (client, token) => UiJson.Write(await client.ReadChartPointsAsync(target, query, token));
	}

	private static Operation BookLevels(UiArguments arguments)
	{
		var target = Node(arguments);
		var side = arguments.Optional("side");
		var query = new OrderBookLevelsQuery(
			side is null ? null : Side(side),
			Page(arguments),
			null);

		return async (client, token) => UiJson.Write(await client.ReadOrderBookLevelsAsync(target, query, token));
	}

	private static Operation PropertyItems(UiArguments arguments)
	{
		var target = Node(arguments);
		var query = new PropertyEditorItemsQuery(arguments.List("paths"), Page(arguments), null);

		return async (client, token)
			=> UiJson.Write(await client.ReadPropertyEditorItemsAsync(target, query, token));
	}

	private static Operation TreeItems(UiArguments arguments)
	{
		var target = Node(arguments);
		var query = new TreeItemsQuery(
			arguments.Optional("parent"),
			arguments.List("keys"),
			Page(arguments),
			null);

		return async (client, token) => UiJson.Write(await client.ReadTreeItemsAsync(target, query, token));
	}

	private static Operation DocumentContent(UiArguments arguments)
	{
		var target = Node(arguments);
		var query = new DocumentContentQuery(arguments.Index("from-line"), Page(arguments), null);

		return async (client, token) => UiJson.Write(await client.ReadDocumentContentAsync(target, query, token));
	}

	private static Operation DiagramNodes(UiArguments arguments)
	{
		var target = Node(arguments);
		var query = new DiagramNodesQuery(arguments.List("keys"), Page(arguments), null);

		return async (client, token) => UiJson.Write(await client.ReadDiagramNodesAsync(target, query, token));
	}

	private static Operation DiagramConnections(UiArguments arguments)
	{
		var target = Node(arguments);
		var query = new DiagramConnectionsQuery(
			arguments.Optional("node-key"),
			arguments.List("keys"),
			Page(arguments),
			null);

		return async (client, token)
			=> UiJson.Write(await client.ReadDiagramConnectionsAsync(target, query, token));
	}

	private static Operation DockLayout(UiArguments arguments)
	{
		var target = Node(arguments);
		var query = new DockLayoutQuery(arguments.Optional("root"), Budget(arguments), null);

		return async (client, token) => UiJson.Write(await client.ReadDockLayoutAsync(target, query, token));
	}

	private static Operation Click(UiArguments arguments)
		=> Input(arguments, new UiClickAction(
			arguments.Choice("button", UiPointerButtons.Left),
			arguments.Number("count", 1)));

	private static Operation Type(UiArguments arguments)
		=> Input(arguments, new UiTextAction(
			arguments.Required("text"),
			arguments.Choice("mode", UiTextModes.Append)));

	private static Operation Key(UiArguments arguments)
		=> Input(arguments, new UiKeyAction(arguments.Required("key"), Modifiers(arguments)));

	private static Operation Scroll(UiArguments arguments)
	{
		var horizontal = arguments.Number("dx", 0);
		var vertical = arguments.Number("dy", 0);

		if (horizontal == 0 && vertical == 0)
			throw new UiUsageException("scroll needs a distance: --dx, --dy or both.");

		return Input(arguments, new UiScrollAction(horizontal, vertical));
	}

	private static Operation ActionStatus(UiArguments arguments)
	{
		var actionId = Identifier(arguments, "action");

		return async (client, token) => UiJson.Write(await client.GetActionStatusAsync(actionId, token));
	}

	private static Operation Wait(UiArguments arguments)
	{
		var request = new UiWaitRequest(
			Node(arguments),
			Condition(arguments),
			TimeSpan.FromSeconds(arguments.Number("timeout", 10)),
			Capture(arguments));

		return async (client, token) => UiJson.Write(await client.WaitAsync(request, token));
	}

	private static Operation Screenshot(UiArguments arguments)
	{
		var request = new UiScreenshotRequest(
			Node(arguments),
			arguments.Optional("kind") ?? UiCaptureKinds.ControlRender,
			arguments.Flag("popups"),
			arguments.Number("max-width", 0),
			arguments.Number("max-height", 0),
			null,
			[.. (arguments.Optional("mask") ?? string.Empty)
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(Mask)]);

		var path = arguments.Optional("out");

		return async (client, token) =>
		{
			var info = await client.CaptureScreenshotAsync(request, token);

			if (path is not null)
				await UiArtifactFile.SaveAsync(client, info.ArtifactId, path, token);

			return UiJson.Write(new UiCapturedScreenshot(info, path));
		};
	}

	private static Operation Artifact(UiArguments arguments)
	{
		var artifactId = arguments.Required("id");
		var path = arguments.Required("out");

		return async (client, token) =>
		{
			var length = await UiArtifactFile.SaveAsync(client, artifactId, path, token);

			return UiJson.Write(new UiSavedArtifact(artifactId, path, length));
		};
	}

	private static Operation Diagnostics(UiArguments arguments)
	{
		var query = new UiDiagnosticQuery(
			arguments.Optional("after"),
			arguments.Number("limit", 100),
			OptionalNodeId(arguments),
			arguments.Optional("action") is null ? null : Identifier(arguments, "action"));

		return async (client, token) => UiJson.Write(await client.ReadDiagnosticsAsync(query, token));
	}

	private static Operation Input(UiArguments arguments, UiInputAction action)
	{
		// The identifier is made here rather than by the endpoint, so that a caller whose connection broke
		// mid-click can ask again with the same one and be told what became of the first attempt.
		var request = new UiInputRequest(
			arguments.Optional("action") is null ? Guid.NewGuid() : Identifier(arguments, "action"),
			Node(arguments),
			Part(arguments),
			action,
			null,
			TimeSpan.FromSeconds(arguments.Number("timeout", 10)));

		return async (client, token) => UiJson.Write(await client.ExecuteInputAsync(request, token));
	}

	private static UiTargetPart Part(UiArguments arguments)
	{
		var kind = arguments.Optional("part") ?? UiControlPart.KindName;

		return kind switch
		{
			UiControlPart.KindName => UiControlPart.Instance,
			UiGridHeaderPart.KindName => new UiGridHeaderPart(arguments.Required("column")),
			UiGridCellPart.KindName => new UiGridCellPart(arguments.Required("row"), arguments.Required("column")),
			UiGridCellControlPart.KindName => new UiGridCellControlPart(
				arguments.Required("row"), arguments.Required("column"), arguments.Required("control")),
			UiPropertyValuePart.KindName => new UiPropertyValuePart(arguments.Required("path")),
			UiPropertyValueControlPart.KindName => new UiPropertyValueControlPart(arguments.Required("path"), arguments.Required("control")),
			UiListItemPart.KindName => new UiListItemPart(
				arguments.Index("index") ?? throw new UiUsageException("--part listItem needs --index.")),
			UiTreeItemPart.KindName => new UiTreeItemPart(arguments.Required("item")),
			UiTreeItemControlPart.KindName => new UiTreeItemControlPart(arguments.Required("item"), arguments.Required("control")),
			UiDockTabPart.KindName => new UiDockTabPart(arguments.Required("panel")),
			UiDockClosePart.KindName => new UiDockClosePart(arguments.Required("panel")),
			UiRibbonItemPart.KindName => new UiRibbonItemPart(arguments.Required("item")),
			UiChartAnnotationPart.KindName => new UiChartAnnotationPart(arguments.Required("annotation")),
			_ => throw new UiUsageException(
				$"--part takes one of {UiControlPart.KindName}, {UiGridHeaderPart.KindName}, " +
				$"{UiGridCellPart.KindName}, {UiGridCellControlPart.KindName}, {UiPropertyValuePart.KindName}, {UiPropertyValueControlPart.KindName}, {UiListItemPart.KindName}, {UiTreeItemPart.KindName}, {UiTreeItemControlPart.KindName}, {UiDockTabPart.KindName}, {UiDockClosePart.KindName}, {UiRibbonItemPart.KindName} or " +
				$"{UiChartAnnotationPart.KindName}, not '{kind}'."),
		};
	}

	private static UiCondition Condition(UiArguments arguments)
	{
		var path = arguments.Optional("field");
		var exists = arguments.Flag("exists");
		var missing = arguments.Flag("missing");

		if (exists && missing)
			throw new UiUsageException("--exists and --missing say opposite things.");

		if (path is not null)
		{
			if (exists || missing)
				throw new UiUsageException("wait takes either a field or an existence, not both.");

			return new UiFieldCondition(path, arguments.Choice("op", UiComparisons.Equal), Value(arguments));
		}

		if (exists || missing)
			return new UiExistsCondition(exists);

		throw new UiUsageException("wait needs a condition: --field with --value, or --exists, or --missing.");
	}

	// The kind is said rather than guessed. "100.5" is a price to one caller and a label to another, and a
	// wait that compared the wrong one would sit there until it timed out with nothing to show for it.
	private static UiValue Value(UiArguments arguments)
	{
		var text = arguments.Required("value");
		var kind = arguments.Optional("value-kind") ?? UiValueKinds.String;

		return kind switch
		{
			UiValueKinds.String => new UiStringValue(text),
			UiValueKinds.Boolean => new UiBooleanValue(Parsed(bool.TryParse(text, out var flag), flag, kind, text)),
			UiValueKinds.Int64 => new UiInt64Value(Parsed(long.TryParse(text, out var whole), whole, kind, text)),
			UiValueKinds.Decimal => new UiDecimalValue(Parsed(
				decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var exact),
				exact, kind, text)),
			UiValueKinds.Double => new UiDoubleValue(Parsed(
				double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real),
				real, kind, text)),
			UiValueKinds.Timestamp => new UiTimestampValue(Parsed(
				DateTime.TryParse(
					text,
					CultureInfo.InvariantCulture,
					DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
					out var moment),
				moment, kind, text)),
			UiValueKinds.Null => UiNullValue.Instance,
			_ => throw new UiUsageException(
				$"--value-kind takes one of {UiValueKinds.String}, {UiValueKinds.Boolean}, {UiValueKinds.Int64}, " +
				$"{UiValueKinds.Decimal}, {UiValueKinds.Double}, {UiValueKinds.Timestamp} or " +
				$"{UiValueKinds.Null}, not '{kind}'."),
		};
	}

	private static T Parsed<T>(bool parsed, T value, string kind, string text)
		=> parsed ? value : throw new UiUsageException($"'{text}' is not {kind}.");

	private static UiKeyModifiers Modifiers(UiArguments arguments)
	{
		var modifiers = UiKeyModifiers.None;

		foreach (var name in arguments.List("modifiers"))
		{
			modifiers |= Enum.TryParse<UiKeyModifiers>(name, ignoreCase: true, out var one) && Enum.IsDefined(one)
				? one
				: throw new UiUsageException(
					$"--modifiers takes shift, control, alt and meta, separated by commas, not '{name}'.");
		}

		return modifiers;
	}

	private static OrderBookSides Side(string text)
		=> text switch
		{
			"bid" or "bids" => OrderBookSides.Bid,
			"ask" or "asks" => OrderBookSides.Ask,
			_ => throw new UiUsageException($"--side takes bids or asks, not '{text}'."),
		};

	private static Guid Identifier(UiArguments arguments, string name)
	{
		var text = arguments.Required(name);

		return Guid.TryParse(text, out var value)
			? value
			: throw new UiUsageException($"--{name} takes an identifier such as {Guid.Empty:D}, not '{text}'.");
	}

	private static UiTarget Node(UiArguments arguments)
		=> OptionalNode(arguments) ?? throw new UiUsageException("--node is required.");

	private static UiTarget OptionalNode(UiArguments arguments)
	{
		var id = OptionalNodeId(arguments);

		return id is null ? null : UiTarget.FromId(id);
	}

	// A handle is deliberately not accepted. It is only meaningful inside the session that was given it,
	// and this program opens a new one every time it runs.
	private static UiNodeId OptionalNodeId(UiArguments arguments)
	{
		var text = arguments.Optional("node");

		if (text is null)
			return null;

		var separator = text.IndexOf('/');

		if (separator <= 0 || separator == text.Length - 1)
			throw new UiUsageException($"--node takes a scope and an identifier, as scope/identifier, not '{text}'.");

		return new UiNodeId(text[..separator], text[(separator + 1)..]);
	}

	// The same scope/identifier spelling as --node, for the list of things to paint over.
	// "scope/identifier" leaves a control out of the picture; "scope/identifier#column" the values of that
	// column of a table, down its height.
	private static UiMask Mask(string text)
	{
		var hash = text.IndexOf('#');

		return hash < 0
			? new(Address(text), null, UiMaskScopes.Node)
			: new(Address(text[..hash]), new UiGridHeaderPart(text[(hash + 1)..]), UiMaskScopes.Node);
	}

	private static UiNodeId Address(string text)
	{
		var separator = text.IndexOf('/');

		if (separator <= 0 || separator == text.Length - 1)
			throw new UiUsageException($"--mask takes a scope and an identifier, as scope/identifier, not '{text}'.");

		return new UiNodeId(text[..separator], text[(separator + 1)..]);
	}

	private static UiPageRequest Page(UiArguments arguments)
		=> new(arguments.Number("limit", UiPageRequest.Default.Limit), arguments.Optional("cursor"));

	private static UiCaptureOptions Capture(UiArguments arguments)
		=> new(arguments.List("fields"), !arguments.Flag("no-layout"), Budget(arguments), null);

	private static UiReadBudget Budget(UiArguments arguments)
		=> new(
			arguments.Number("max-depth", UiReadBudget.Default.MaxDepth),
			arguments.Number("max-nodes", UiReadBudget.Default.MaxNodes),
			arguments.Number("max-items", UiReadBudget.Default.MaxItems),
			arguments.Number("max-bytes", UiReadBudget.Default.MaxBytes));
}
