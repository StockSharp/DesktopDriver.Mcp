namespace StockSharp.DesktopDriver.Mcp;

using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol;
using ModelContextProtocol.Server;

using StockSharp.DesktopDriver.Input;
using StockSharp.DesktopDriver.Queries;
using StockSharp.DesktopDriver.Values;
using StockSharp.DesktopDriver.Waiting;

/// <summary>
/// Tools that drive an application the way a person would.
/// </summary>
/// <remarks>
/// Input is a real click and a real keystroke on the machine's own desktop, so the application sees
/// exactly what it would see from a person. That also means it needs the desktop: a machine with no
/// session attached refuses rather than pretending.
/// <para>
/// "Dispatched" means the click was delivered, and nothing more. Whether the button did what was hoped
/// is a separate question, asked with ui_snapshot or ui_wait.
/// </para>
/// </remarks>
[McpServerToolType]
public static class InputTools
{
	[McpServerTool(Name = "ui_click", Title = "Click something")]
	[Description(
		"Clicks a node, or a part of one: a column header, a cell, a panel's tab or its close button. " +
		"Answers with a receipt saying what became of the click. It being delivered does not mean the " +
		"application did what was hoped — check that with ui_snapshot or ui_wait.")]
	public static Task<string> ClickAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("Which node, as scope/identifier.")] string node,
		[Description("Left, Middle or Right.")] UiPointerButtons button = UiPointerButtons.Left,
		[Description("How many clicks; 2 for a double click. 1 by default.")] int count = 1,
		[Description("A part of it: gridHeader, gridCell, gridCellControl, propertyValue, propertyValueControl, listItem, treeItem, treeItemControl, dockTab, dockClose or chartAnnotation.")] string part = null,
		[Description("Which column, for gridHeader, gridCell and gridCellControl.")] string columnId = null,
		[Description("Which row, for gridCell and gridCellControl.")] string rowKey = null,
		[Description("The name of the control inside the cell, the tree item or the property's editor, for gridCellControl, treeItemControl and propertyValueControl.")] string controlName = null,
		[Description("The property's path on the object the editor shows, for propertyValue and propertyValueControl.")] string propertyPath = null,
		[Description("The item's path through the tree, for treeItem and treeItemControl.")] string itemKey = null,
		[Description("The item's position in the list, from 0, for listItem.")] long? itemIndex = null,
		[Description("Which panel, for dockTab and dockClose.")] string panelId = null,
		[Description("Which annotation, for chartAnnotation.")] string annotationId = null,
		CancellationToken cancellationToken = default)
		=> SendAsync(
			applications,
			instance,
			node,
			Part(part, columnId, rowKey, controlName, propertyPath, itemKey, itemIndex, panelId, annotationId),
			new UiClickAction(button, count > 0 ? count : 1),
			cancellationToken);

	[McpServerTool(Name = "ui_type_text", Title = "Type into something")]
	[Description(
		"Types text into a node. Append adds to what is there; Replace clears it first. The text is " +
		"typed as characters, so the application's own input handling runs exactly as it would for a " +
		"person.")]
	public static Task<string> TypeTextAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("Which node, as scope/identifier.")] string node,
		[Description("What to type.")] string text,
		[Description("Append or Replace.")] UiTextModes mode = UiTextModes.Append,
		CancellationToken cancellationToken = default)
		=> SendAsync(
			applications,
			instance,
			node,
			UiControlPart.Instance,
			new UiTextAction(text, mode),
			cancellationToken);

	[McpServerTool(Name = "ui_press_key", Title = "Press a key")]
	[Description(
		"Presses one key at a node, optionally with modifiers. Keys are named as Avalonia names them: " +
		"Enter, Escape, Tab, Delete, Back, Up, Down, Left, Right, Home, End, PageUp, PageDown, F1 to " +
		"F12, A to Z, D0 to D9.")]
	public static Task<string> PressKeyAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("Which node, as scope/identifier.")] string node,
		[Description("Which key.")] string key,
		[Description("Held down with it: Shift, Control, Alt, Meta, separated by commas.")] string modifiers = null,
		CancellationToken cancellationToken = default)
		=> SendAsync(
			applications,
			instance,
			node,
			UiControlPart.Instance,
			new UiKeyAction(key, Modifiers(modifiers)),
			cancellationToken);

	[McpServerTool(Name = "ui_scroll", Title = "Scroll something")]
	[Description("Scrolls a node. Distances are in wheel notches; negative scrolls down and right.")]
	public static Task<string> ScrollAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("Which node, as scope/identifier.")] string node,
		[Description("Sideways, in notches.")] double deltaX = 0,
		[Description("Up and down, in notches.")] double deltaY = 0,
		CancellationToken cancellationToken = default)
	{
		if (deltaX == 0 && deltaY == 0)
			throw new McpException("ui_scroll needs a distance: deltaX, deltaY or both.");

		return SendAsync(
			applications,
			instance,
			node,
			UiControlPart.Instance,
			new UiScrollAction(deltaX, deltaY),
			cancellationToken);
	}

	[McpServerTool(Name = "ui_bring_into_view", Title = "Bring a part onto the screen")]
	[Description(
		"Brings a part of a control onto the screen so that it can be clicked - a row far down a table, " +
		"a tab scrolled off the strip. A grid builds visuals only for what it is showing, so a row a " +
		"thousand down has nothing to click until this has run. Afterwards look the part up again by its " +
		"key: everything on screen has moved. This uses the control's own positioning and sends no " +
		"input, so it proves nothing about whether scrolling works.")]
	public static Task<string> BringIntoViewAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("Which node, as scope/identifier.")] string node,
		[Description("Which part: gridHeader, gridCell, gridCellControl, propertyValue, propertyValueControl, listItem, treeItem, treeItemControl, dockTab, dockClose or chartAnnotation.")] string part = null,
		[Description("Which column, for gridHeader, gridCell and gridCellControl.")] string columnId = null,
		[Description("Which row, for gridCell and gridCellControl.")] string rowKey = null,
		[Description("The name of the control inside the cell, the tree item or the property's editor, for gridCellControl, treeItemControl and propertyValueControl.")] string controlName = null,
		[Description("The property's path on the object the editor shows, for propertyValue and propertyValueControl.")] string propertyPath = null,
		[Description("The item's path through the tree, for treeItem and treeItemControl.")] string itemKey = null,
		[Description("The item's position in the list, from 0, for listItem.")] long? itemIndex = null,
		[Description("Which panel, for dockTab and dockClose.")] string panelId = null,
		[Description("Which annotation, for chartAnnotation.")] string annotationId = null,
		CancellationToken cancellationToken = default)
		=> SendAsync(
			applications,
			instance,
			node,
			Part(part, columnId, rowKey, controlName, propertyPath, itemKey, itemIndex, panelId, annotationId),
			UiEnsureVisibleAction.Instance,
			cancellationToken);

	[McpServerTool(Name = "ui_action_status", Title = "What became of an input", ReadOnly = true)]
	[Description(
		"What became of an input already sent, by the action identifier its receipt carried. Worth " +
		"asking when a send failed on the way back and it is not clear whether it happened.")]
	public static Task<string> ActionStatusAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("The action identifier from an earlier receipt.")] string actionId,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var id = UiAnswers.Identifier(actionId, "an action");

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.GetActionStatusAsync(id, cancellationToken)));
	}

	[McpServerTool(Name = "ui_wait", Title = "Wait for something to happen", ReadOnly = true)]
	[Description(
		"Waits until a node satisfies a condition, or until the time runs out. Either that the node is " +
		"there or is not, or that one of the fields ui_snapshot reports compares as asked. The field is " +
		"named by its path in that answer, such as rowCount or bestBidPrice. Say which kind the value " +
		"is: guessing between a price and a label gets it wrong silently and the wait simply times out.")]
	public static Task<string> WaitAsync(
		UiApplications applications,
		[Description("Which instance.")] string instance,
		[Description("Which node, as scope/identifier.")] string node,
		[Description("A field from ui_snapshot; leave empty to wait for the node itself.")] string field = null,
		[Description("How to compare: Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual, Contains.")] UiComparisons comparison = UiComparisons.Equal,
		[Description("What to compare against.")] string value = null,
		[Description("What kind it is: string, boolean, int64, decimal, double, timestamp, null.")] string valueKind = null,
		[Description("When no field is given: true to wait for the node to be there, false for it to be gone.")] bool expectedToExist = true,
		[Description("How long to wait, in seconds; 10 by default.")] int timeoutSeconds = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applications);

		var request = new UiWaitRequest(
			UiAnswers.Node(node),
			string.IsNullOrEmpty(field)
				? new UiExistsCondition(expectedToExist)
				: new UiFieldCondition(field, comparison, Value(value, valueKind)),
			TimeSpan.FromSeconds(timeoutSeconds > 0 ? timeoutSeconds : 10),
			UiCaptureOptions.Default);

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.WaitAsync(request, cancellationToken)));
	}

	private static Task<string> SendAsync(
		UiApplications applications,
		string instance,
		string node,
		UiTargetPart part,
		UiInputAction action,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(applications);

		// The identifier is made before the send, so that a send whose answer was lost can be asked about
		// with ui_action_status instead of being repeated into a second click.
		var request = new UiInputRequest(
			Guid.NewGuid(),
			UiAnswers.Node(node),
			part,
			action,
			null,
			TimeSpan.FromSeconds(10));

		return applications.AskAsync(instance, async client
			=> UiAnswers.Json(await client.ExecuteInputAsync(request, cancellationToken)));
	}

	private static UiTargetPart Part(
		string part,
		string columnId,
		string rowKey,
		string controlName,
		string propertyPath,
		string itemKey,
		long? itemIndex,
		string panelId,
		string annotationId)
		=> string.IsNullOrEmpty(part) ? UiControlPart.Instance : part switch
		{
			UiControlPart.KindName => UiControlPart.Instance,
			UiGridHeaderPart.KindName => new UiGridHeaderPart(Needed(columnId, "columnId", part)),
			UiGridCellPart.KindName => new UiGridCellPart(
				Needed(rowKey, "rowKey", part), Needed(columnId, "columnId", part)),
			UiGridCellControlPart.KindName => new UiGridCellControlPart(
				Needed(rowKey, "rowKey", part), Needed(columnId, "columnId", part), Needed(controlName, "controlName", part)),
			UiPropertyValuePart.KindName => new UiPropertyValuePart(Needed(propertyPath, "propertyPath", part)),
			UiPropertyValueControlPart.KindName => new UiPropertyValueControlPart(Needed(propertyPath, "propertyPath", part), Needed(controlName, "controlName", part)),
			UiListItemPart.KindName => new UiListItemPart(itemIndex ?? throw new McpException($"Clicking a {part} needs itemIndex.")),
			UiTreeItemPart.KindName => new UiTreeItemPart(Needed(itemKey, "itemKey", part)),
			UiTreeItemControlPart.KindName => new UiTreeItemControlPart(Needed(itemKey, "itemKey", part), Needed(controlName, "controlName", part)),
			UiDockTabPart.KindName => new UiDockTabPart(Needed(panelId, "panelId", part)),
			UiDockClosePart.KindName => new UiDockClosePart(Needed(panelId, "panelId", part)),
			UiChartAnnotationPart.KindName => new UiChartAnnotationPart(
				Needed(annotationId, "annotationId", part)),
			_ => throw new McpException(
				$"There is no part called '{part}'. The parts are {UiControlPart.KindName}, " +
				$"{UiGridHeaderPart.KindName}, {UiGridCellPart.KindName}, {UiGridCellControlPart.KindName}, {UiPropertyValuePart.KindName}, {UiPropertyValueControlPart.KindName}, {UiListItemPart.KindName}, {UiTreeItemPart.KindName}, {UiTreeItemControlPart.KindName}, {UiDockTabPart.KindName}, " +
				$"{UiDockClosePart.KindName} and {UiChartAnnotationPart.KindName}."),
		};

	private static string Needed(string value, string name, string part)
		=> string.IsNullOrEmpty(value)
			? throw new McpException($"Clicking a {part} needs {name}.")
			: value;

	private static UiKeyModifiers Modifiers(string modifiers)
	{
		var held = UiKeyModifiers.None;

		foreach (var name in UiAnswers.List(modifiers))
		{
			held |= Enum.TryParse<UiKeyModifiers>(name, ignoreCase: true, out var one) && Enum.IsDefined(one)
				? one
				: throw new McpException(
					$"There is no modifier called '{name}'. They are Shift, Control, Alt and Meta.");
		}

		return held;
	}

	// The kind is said rather than guessed. "100.5" is a price to one caller and a label to another, and
	// a wait that compared the wrong one would sit there until it timed out with nothing to show for it.
	private static UiValue Value(string text, string kind)
	{
		if (text is null)
			throw new McpException("ui_wait on a field needs a value to compare it against.");

		return (string.IsNullOrEmpty(kind) ? UiValueKinds.String : kind) switch
		{
			UiValueKinds.String => new UiStringValue(text),
			UiValueKinds.Boolean => new UiBooleanValue(Parsed(bool.TryParse(text, out var flag), flag, text, "a boolean")),
			UiValueKinds.Int64 => new UiInt64Value(Parsed(long.TryParse(text, out var whole), whole, text, "a whole number")),
			UiValueKinds.Decimal => new UiDecimalValue(Parsed(
				decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var exact),
				exact, text, "a decimal")),
			UiValueKinds.Double => new UiDoubleValue(Parsed(
				double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real),
				real, text, "a double")),
			UiValueKinds.Timestamp => new UiTimestampValue(Parsed(
				DateTime.TryParse(
					text,
					CultureInfo.InvariantCulture,
					DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
					out var moment),
				moment, text, "a UTC time")),
			UiValueKinds.Null => UiNullValue.Instance,
			_ => throw new McpException(
				$"There is no kind called '{kind}'. They are {UiValueKinds.String}, {UiValueKinds.Boolean}, " +
				$"{UiValueKinds.Int64}, {UiValueKinds.Decimal}, {UiValueKinds.Double}, " +
				$"{UiValueKinds.Timestamp} and {UiValueKinds.Null}."),
		};
	}

	private static T Parsed<T>(bool parsed, T value, string text, string what)
		=> parsed ? value : throw new McpException($"'{text}' is not {what}.");
}
