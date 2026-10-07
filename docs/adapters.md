# What answers for which control

An adapter is the only place that knows a particular control. Everything above it — the registry, the
tree, the endpoint, the client, the command line, the agent tools — is the same for every control, and
that is the point: a table never has to know it is docked, and the docking never has to know what a
table is.

## The adapters the driver carries

| adapter | package | what it answers for | kind | what it adds |
|---|---|---|---|---|
| `AvaloniaControlAdapter` | `Avalonia` | any Avalonia control | `control` and the rest of the plain kinds | text, value, selected, checked, read-only, expanded, the selected item, the active tab |
| `AvaloniaTreeAdapter` | `Avalonia` | `TreeView` | `tree` | `tree.items` |
| `DataGridAdapter` | `Avalonia.ProDataGrid` | `DataGrid` and everything built on it | `grid` | `grid.columns`, `grid.rows`, `grid.groups` |
| `DockAdapter` | `Avalonia.Dock` | `DockControl` | `workspace` | `dock.layout` |

WPF and MAUI each carry the same plain adapters for their standard controls. An application registers
adapters for its own controls through the same interface: the StockSharp applications, for instance, read
their charts, order books, property grids, diagrams, code editors and panels through modules that live in
their own repository and are handed to the bootstrap at start-up.

The plain adapter is registered at the lowest priority, so anything written for a particular control wins
over it. A control nobody has written an adapter for still answers what every control can answer, which
is more useful than refusing to describe it.

A more specific registration wins over a less specific one: an order book drawn as a table is registered
above the grid, and a diagram editor that contains a surface is registered above it.

## Two things are said, not one

A control's **kind** and its **adapter** answer different questions: the kind decides what a node is
called in the tree, and the adapter decides what can be asked of it. Both are declared in the module that
owns the control, because the binder cannot name types it does not reference — a grid whose kind still
read `control` would be skipped by anything looking for grids.

Kind rules are asked in the order they were registered. A module for a control derived from one the
driver reads registers its own rule before it brings the driver's module, so that the more specific kind
is the one a node gets.

## What an adapter is allowed to do

**Read, and nothing else.** It does not create a panel that has never been shown, does not expand a
property that is closed, does not open an item in a tree, does not scroll a row into view and does not
ask a control for something that would build it. Where that means an answer cannot be given, it says so:

- a property nobody has opened reports `notCreated` for the count of what is inside it, rather than nought;
- a cell that has not been drawn reports `notCreated` for its bounds, rather than an empty rectangle;
- a control with no notion of sorting reports `unsupported`, rather than "ascending".

**Say what was drawn, not what was fed in.** A chart answers with the points it drew; a diagram with the
blocks the surface presented and the polyline it routed. A chart that quietly drew nothing is exactly the
fault worth catching, and reading the source instead would pass while the screen was empty.

**Name things by what they are.** A row is named by a key, a column by its own identifier, a level by its
side and price, a property by its path, a tree item by its path, a diagram block by its key. A position
changes with every sort, filter, quote and drag; none of those change what the thing is.

**Answer one page at a time.** Every paged read takes a budget and says when it stopped short. A read
that hit a bound says so; it never returns a page that looks complete.

## Adding one

For a control of the application's own, steps 4 and 6 are all there is: the module goes into the list the
application hands its bootstrap. A new kind of reading - a new state, a new paged query - is a change to
the driver itself:

1. **The shape**, in `StockSharp.DesktopDriver.Contracts`: a `UiState` for what the control is, a snapshot
   record for what it holds one of, and a query record for which ones. Register the state in
   `UiStateRegistry`, add the method name to `UiMethods` and its arguments to `UiMethodParams`.
2. **The contract**, in `StockSharp.DesktopDriver.Runtime`: an `IUi…Adapter` interface with the read on it,
   and one method on `UiAutomationService` that resolves it and raises `unsupportedCapability` when the
   node's adapter is not one.
3. **The wire**: one branch in `UiRequestDispatcher` and one method on `UiAutomationClient`.
4. **The adapter**, in the layer that owns the control, deriving from the toolkit's control adapter so
   that everything common still answers. Register it in that layer's module, with a kind rule beside it.
5. **The shells**: one branch in the command line's operation list, one tool in the MCP server.
6. **The tests**: against the real control, in a real window, filled through its own public API. A
   control built for the test would answer questions about itself rather than about what the product
   shows.

## Parts of a control

A caller says "the Price header" or "the Volume cell of that trade", never a coordinate. An adapter that
knows where its own parts are implements `IUiInputTargetAdapter`: it finds the visual and hands it to
`AvaloniaInputTargetResolver.Locate`, so reaching a header is decided the same way as reaching any other
control — by asking the window what is actually under the point, and refusing one that lands on something
unrelated.

| part | resolved by | what it names |
|---|---|---|
| `gridHeader` | `DataGridAdapter` | a column's header |
| `gridCell` | `DataGridAdapter` | a record's cell in a column |
| `gridCellControl` | `DataGridAdapter` | a named control inside a record's cell |
| `treeItem` / `treeItemControl` | `AvaloniaTreeAdapter` | an item of a tree, and a named control inside it |
| `dockTab` / `dockClose` | `DockAdapter` | a panel's tab, and the close button on it |
| `ribbonItem` | the product's adapter for its bars | an entry of a ribbon, a menu or a tool bar, by the name its markup gave it |

A grid builds visuals only for what it is showing, so a part outside the view has nothing to click. The
`ensureVisible` action asks the control to bring it onto the screen through the control's own positioning;
it sends no input, so it establishes that the part is reachable and nothing about scrolling. Afterwards
the part is looked up again by its key, because everything on screen has moved.

## Scopes

Everything inside a panel, a dialog or a document is addressed relative to it, so a table keeps its
address when the panel is dragged into another window. A control can declare that it is the root of a
scope with `UiAutomationNames.SetScopeId`; a whole family of them is better answered once by a rule
registered through `UiAutomationScopes.Add` by the module that knows the family. A build without that
module behaves as though the family did not exist.

## When the control has to help

Sometimes what a picture of the screen shows is not reachable from outside the control - a chart whose
data lives in an internal document, a search box that narrows a property grid without touching its model.
Then the control gets a read-only accessor, and only that: one that answers what was drawn and changes
nothing. An adapter that reached in with reflection instead would be reading private state nobody
promised to keep, and would break silently the first time the control was refactored.
