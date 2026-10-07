namespace StockSharp.DesktopDriver.Cli;

/// <summary>
/// What this program answers when asked how to use it.
/// </summary>
internal static class UiUsage
{
	public const string Text = """
		desktop-driver <operation> [options]

		Reads and drives a desktop application that was started to be driven. The answer is the
		protocol's own JSON on standard output; anything else goes to standard error.

		Reaching the application
		  --endpoint <path>      the endpoint file the application wrote (or STOCKSHARP_UI_ENDPOINT)
		  --app <appId>          the product expected to answer; defaults to what the endpoint says
		  --connect-timeout <s>  how long to wait for the connection; 15 by default

		Naming a node
		  --node <scope>/<id>    a node's address, as `find`, `tree` and `windows` report it

		Reading
		  session                                what answered and what it can do
		  windows                                the windows and popups on screen
		  find     --name|--kind|--automation-id|--text|--scope|--surface [--limit]
		  tree     [--node] [--visual-internals] [--max-depth] [--max-nodes]
		  snapshot --node [--fields a,b] [--no-layout]
		  grid-columns   --node [--columns a,b] [--limit]
		  grid-rows      --node [--mode viewData|viewport] [--start N] [--rows k1,k2] [--columns a,b]
		  grid-groups    --node [--parent <groupId>] [--limit]
		  chart-series   --node [--limit]
		  chart-points   --node --series <id> [--start N] [--from <time>] [--to <time>] [--keys k1,k2]
		  book-levels    --node [--side bids|asks] [--limit]
		  property-items --node [--paths a.b,c] [--limit]
		  tree-items     --node [--parent <itemKey>] [--keys k1,k2] [--limit]
		  document-content --node [--from-line N] [--limit]
		  diagram-nodes  --node [--keys k1,k2] [--limit]
		  diagram-connections --node [--node-key <blockKey>] [--keys k1,k2] [--limit]
		  dock-layout    --node [--root <layoutId>]
		  diagnostics    [--after <cursor>] [--limit N] [--node] [--action <id>]

		Driving
		  click  --node [--button left|middle|right] [--count N] [--part ...]
		  type   --node --text <text> [--mode append|replace]
		  key    --node --key <name> [--modifiers shift,control,alt,meta]
		  scroll --node [--dx N] [--dy N]
		  show   --node --part ...
		  action-status --action <id>
		  wait   --node (--field <path> --value <v> [--value-kind <k>] [--op <comparison>] | --exists | --missing)
		         [--timeout <s>]

		  A part other than the whole control: --part gridHeader --column <id>;
		  --part gridCell --row <key> --column <id>; --part gridCellControl --row <key> --column <id>
		  --control <name>; --part propertyValue --path <property>; --part propertyValueControl --path <property> --control <name>; --part listItem --index <n>;
		  --part treeItem --item <key>;
		  --part treeItemControl --item <key> --control <name>; --part dockTab|dockClose --panel <id>;
		  --part ribbonItem --item <name>; --part chartAnnotation --annotation <id>.

		  show brings a part onto the screen so that it can be clicked, through the control's own
		  positioning. A row a thousand down has no visual until then - and a different one afterwards,
		  so look it up again by its key rather than reusing what you had.

		  --action <id> on an input repeats a send that may already have happened: the same identifier
		  returns the first receipt instead of clicking twice.

		Pictures and files
		  screenshot --node [--kind controlRender|windowCapture|screenCapture] [--popups]
		             [--max-width N] [--max-height N] [--out <file>]
		             [--mask scope/id,scope/grid#column] - blurred before the picture is written
		  artifact   --id <artifactId> --out <file>

		Exit codes
		  0 the operation ran        3 the application could not be reached
		  1 something went wrong     4 the application refused the operation
		  2 the command line is wrong

		A click that was delivered exits 0 whether or not the button did what you hoped. That is a
		separate question, asked with a read.
		""";
}
