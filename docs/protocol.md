# The protocol

What an application answers, and how a caller asks it. One page, because there is one list of
operations and everything — the C# client, the command line, the agent tools — goes through it.

The types named here are the records in `StockSharp.DesktopDriver.Contracts`. They are the schema: nothing writes
its own JSON model of them, and a test, the client and the endpoint all serialise the same types with
the same converters. A hand-written model on either side would be a second opinion about the protocol,
and the two would disagree the first time one changed.

## Getting to an application

An application listens only when it was started to be driven:

```sh
<product>.exe --ui-automation --ui-automation-endpoint=<path>
```

The channel is a named pipe restricted to the current user, named
`stocksharp.ui.<appId>.<instanceId:N>`. The host and client use `PipeOptions.CurrentUserOnly`; access is
limited by the OS to the current user. Operations are answered only after `session.open` succeeds.

The endpoint file is written once the interface has been laid out and drawn, which is the signal that
the application is ready to be driven. It carries `UiEndpointInfo` — the product, the running copy, the
pipe name, the protocol version and the process.

## The envelope

Every message is four bytes of little-endian length followed by that many bytes of UTF-8 JSON. A
message larger than 1 MB is refused rather than allocated.

A request is `UiRequestEnvelope`:

| field | meaning |
|---|---|
| `protocolVersion` | the version the caller speaks; only the part before the dot has to match |
| `requestId` | this request, so its reply can be matched to it |
| `instanceId` | which running copy of the product the caller means |
| `method` | one of the names below, and nothing else |
| `timeoutMs` | how long the caller will wait; 0 means no deadline of its own |
| `parameters` | the method's own arguments |

A reply is `UiResponseEnvelope` and carries exactly one of `result` or `error`.

### How much a reply may carry

A session has a read budget, and `maxBytes` of it — 256 KB by default — is the size of one serialised
reply. The wire carries 1 MB at most, so the budget that applies is the smaller of the two. An answer
over it comes back as `limitExceeded` naming the size and the limit, rather than being written until the
framing fails: a caller that lost the pipe cannot tell an oversized answer from a crashed application,
and retries the read that broke it. The handshake is exempt — it is where the caller learns the limit.

The way under the limit is to ask for less: a smaller page, fewer columns, a narrower subtree. A page
that was cut short hands back `nextCursor`, and sending it again continues where it stopped. A cursor is
opaque and belongs to one read: it carries the operation and a fingerprint of what the page was of,
including the grid's own sort, grouping and filter, so continuing it after a column header was clicked is
refused rather than splicing two orders into one answer.

The instance is named in every request rather than assumed from the connection. Two copies of a product
are running as often as not, and input sent to the wrong one is a click somebody sees.

## Opening a session

`session.open` takes `UiSessionOpenParams` — the product the caller expects, the running copy it expects,
and its protocol version — and answers with `UiSessionInfo`. It is refused with `notFound` for the wrong
product or the wrong copy, and `protocolMismatch` for another major version. A refusal leaves the
connection without an open session; other operations are refused with `unauthorized` until a handshake
succeeds.

Opening a session allows reads. Input is still refused when the application has real external
connections and was started without a test profile.

`UiSessionInfo` says what answered and what it can do: `appId`, `instanceId`, the product and protocol
versions, the Avalonia and framework versions, the fixture, whether the outside world has been replaced,
the input backend, the capabilities and the default read budget.

## The operations

| method | parameters | result |
|---|---|---|
| `session.info` | — | `UiSessionInfo` |
| `windows` | — | `UiSurfaceInfo[]` |
| `find` | `UiFindQuery` | `UiFindResult` |
| `tree` | `UiTreeQuery` | `UiTreeSnapshot` |
| `snapshot` | `UiCaptureParams` | `UiNodeSnapshot` |
| `grid.columns` | `UiGridColumnsParams` | `UiDataPage<GridColumnSnapshot>` |
| `grid.rows` | `UiGridRowsParams` | `UiDataPage<GridRowSnapshot>` |
| `grid.groups` | `UiGridGroupsParams` | `UiDataPage<GridGroupSnapshot>` |
| `chart.series` | `UiChartSeriesParams` | `UiDataPage<ChartSeriesSnapshot>` |
| `chart.points` | `UiChartPointsParams` | `UiDataPage<ChartPointSnapshot>` |
| `orderBook.levels` | `UiOrderBookLevelsParams` | `UiDataPage<OrderBookLevelSnapshot>` |
| `propertyEditor.items` | `UiPropertyEditorItemsParams` | `UiDataPage<PropertyItemSnapshot>` |
| `tree.items` | `UiTreeItemsParams` | `UiDataPage<TreeItemSnapshot>` |
| `document.content` | `UiDocumentContentParams` | `UiDataPage<DocumentLineSnapshot>` |
| `diagram.nodes` | `UiDiagramNodesParams` | `UiDataPage<DiagramNodeSnapshot>` |
| `diagram.connections` | `UiDiagramConnectionsParams` | `UiDataPage<DiagramConnectionSnapshot>` |
| `dock.layout` | `UiDockLayoutParams` | `DockLayoutSnapshot` |
| `input` | `UiInputRequest` | `UiActionReceipt` |
| `action.status` | `UiActionStatusParams` | `UiActionReceipt` |
| `wait` | `UiWaitRequest` | `UiWaitResult` |
| `screenshot` | `UiScreenshotRequest` | `UiScreenshotInfo` |
| `artifact.read` | `UiArtifactReadRequest` | `UiArtifactChunk` |
| `diagnostics` | `UiDiagnosticQuery` | `UiDiagnosticPage` |
| `request.cancel` | `UiCancelParams` | — |

The list is closed. A method name arriving on the wire is looked up here and nowhere else: an endpoint
that turned a string into a call by reflection would let whoever can reach the pipe run anything the
process can.

## Addressing a node

`UiTarget` names a node either by its address or by a handle it was given, never both and never neither.
An address is `UiNodeId(scopeId, localId)` — the panel, document or window it belongs to, and its
identifier inside that scope. The address survives everything the user can do to the layout: moving a
panel to another dock group, floating it out, and the visual behind it being rebuilt.

A node that nothing has read yet is still addressable. The module searches the live interface for it,
binding what it passes, and refuses with `notFound` when the interface has no such node — it never
returns the nearest node it passed.

## Values that may not be there

Every field that could be missing is a `UiField<T>`: either a value that was actually read, or a reason
there is none — `unsupported`, `notCreated`, `notLoaded`, `redacted` or `unknown`. The protocol avoids
the one answer that cannot be checked, a default standing in for a value nobody could read. A control
with no notion of sorting says so rather than answering "ascending".

Values travel typed — `UiValue` is null, boolean, int64, decimal, double, string or timestamp. A price
compared as a string passes when the control formats it differently and fails when the locale changes,
and a decimal that survived the trip as a double is no longer the price that was on screen.

## Reading is bounded

`UiReadBudget` caps a read at a depth, a node count, an item count and a size. Every paged operation
returns `UiDataPage<T>` with what was read, the total, whether it was truncated and why. A read that
hit a bound says so; it never returns a page that looks complete.

Reading never changes what it reads. It does not create a panel that has not been shown, does not
expand a property that is closed, and does not scroll a row into view.

## The domain schemas

Three families answer through the shared shapes above — `BasicState` for any control, `GridState` for a
table, `ChartState` for a chart, `DockState` for a workspace. Five more have schemas of their own:

| schema | operations | what it adds |
|---|---|---|
| `OrderBookState` | `orderBook.levels` | best prices, spread, counts and volume per side, crossed; levels as side/price/volume/orderCount, keyed by side and price |
| `TreeState` | `tree.items` | the counts of what is on show, open and selected; items by path, with text, depth, open, selected and how many are inside |
| `DocumentState` | `document.content` | the kind and language, the line and character counts, the caret, the selection, read-only and what the editor is complaining about; lines numbered from one |
| `DiagramState` | `diagram.nodes`, `diagram.connections` | the counts of blocks and connections, the selection, the zoom and the area drawn; blocks by key with kind, caption, sockets and bounds; connections by what they join, with the route drawn |
| `PropertyEditorState` | `propertyEditor.items` | the object being edited, categorised and basic modes, the search text, whether anything holds a non-default value and the validation messages; properties by path, with typed value, display text, editor kind, read-only, expanded, non-default and per-property error |

Every schema §9.3 of the specification asks for is now here.

What each of the newer three is careful about:

- a **tree** item is named by its path — "Strategies/Sma/Errors" - because a position changes with every
  item opened above it; two siblings that read the same get a number. Nothing opens an item: a tree
  builds what is inside one when it is opened, so asking would be opening;
- a **document** is read as numbered lines, from one, the way an editor's own margin numbers them, and
  the text comes from the control rather than from whatever was bound into it;
- a **diagram** answers with what the surface drew. A block the composition holds and the surface never
  presented is absent, which is the fault a test about a diagram is looking for; a connection carries
  the polyline that was actually routed around the blocks in the way, not a straight line between two
  sockets.

## When something goes wrong

An error is `UiError` with a code from a closed list. A test decides what to do from the code and never
from the message: messages are written for people and get rewritten, and a test that matched on one
would fail the day somebody improved the wording.

| code | when |
|---|---|
| `unauthorized` | the connection has not completed the session handshake |
| `protocolMismatch` | the two sides do not speak the same version |
| `invalidRequest` | malformed, or an operation that does not exist |
| `notFound` | no such node, product or running copy |
| `ambiguousElement` / `ambiguousAdapter` | a selector matched more than one node; two adapters claim one control |
| `staleElement` / `notCreated` | the handle names a visual that is gone; the node has no visual yet |
| `notInteractable` | the node exists but would not take this input |
| `unsupportedCapability` | the node cannot do what was asked |
| `stateChanged` | something moved under the read guard |
| `timeout` / `cancelled` | ran out of time; the caller cancelled |
| `uiUnavailable` | the interface could not be read at all |
| `inputUnavailable` / `inputBlocked` | no way to send input here; something outside the application is holding the input system |
| `limitExceeded` | a limit of the protocol or the session was reached |
| `applicationExited` | the application closed the connection |
| `actionConflict` | an action identifier was reused for something else |
| `outcomeUnknown` | input was sent and what became of it cannot be established |
| `artifactNotFound` / `artifactExpired` | the picture or file asked for is not there, or no longer is |

## Who speaks this

Three things, and nothing else writes its own JSON model of any of it:

| caller | what it is |
|---|---|
| `StockSharp.DesktopDriver.Client` | the C# client a test or a runner uses in process |
| `StockSharp.DesktopDriver.Cli` | one operation per run from a command line — see [cli.md](cli.md) |
| `StockSharp.DesktopDriver.Mcp` | the agent tools over MCP — see [the server's README](../DesktopDriver.Mcp/README.md) |

The last two are shells over the first. Neither adds an operation of its own, and neither reshapes an
answer: what comes out of the command line is the same JSON the client received.

## Input

`input` takes a `UiInputRequest` — an action identifier the caller generates before the first send, the
node, the part of it, the action, an optional read guard and a deadline.

`UiActionReceipt.status` says what became of it. `dispatched` means the backend finished the sequence;
it does not mean a business action succeeded, and the caller checks that separately with a snapshot or a
wait. The same action identifier sent twice returns the original receipt rather than clicking again: a
runner whose connection broke mid-click has to be able to ask again without the click happening twice.
