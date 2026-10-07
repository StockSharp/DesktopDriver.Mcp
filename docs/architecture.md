# How the driver is built

## Layers

```text
Contracts            the protocol: requests, answers, states, addresses; no toolkit, no business types
   ^
Runtime              registry, snapshots, the semantic tree, revisions, waiting, budgets; no toolkit
   ^
Avalonia | Wpf | Maui            one toolkit each: executor, roots, presentation, standard adapters,
   ^                             input and pictures
Avalonia.ProDataGrid, Avalonia.Dock, the application's own modules
   ^
Bootstrap.Avalonia | Bootstrap.Wpf | Bootstrap.Maui     compose the above around a running application
   ^                                                    and open the endpoint through Host

Host -> Runtime                  the named-pipe endpoint inside the application
Client -> Contracts              the typed client
Runner -> Client                 starts and reaches applications; the catalogue
Cli, Mcp, Testing -> Runner      the command line, the MCP server, the MSTest helpers
```

A control library never references its adapter: the adapter references the library. A library that
referenced its own adapter would close a cycle the moment the adapter referenced the control.

## Inside the application

An application that can be driven references the bootstrap of its toolkit and calls it once, after its
first window exists, when it was started with `--ui-automation` (see `UiLaunchProtocol` and
`UiAutomationLaunch`). The bootstrap registers the toolkit's standard adapters and the modules the
application hands it, and gives `UiAutomationComposition` the toolkit's parts - its executor, roots,
readers, input backend and pictures (`UiToolkitParts`). The composition holds everything that is the
same for every toolkit: the registries, the snapshots, the tree, the input policy, the session and the
endpoint, whose address the bootstrap then writes to the file the runner named.

Nothing of this exists in a build that does not reference the bootstrap, which is how an application
ships an ordinary build with nothing listening in it.

Input is let through only when `UiAutomationStartup.For` says the run is safe: an application that talks
to nothing outside its own process always is, one that connects to something real only on a test profile
that replaces it.

## Outside it

A runner connects to the local pipe, checks the product, instance and protocol in the session handshake,
asks, and acts. The pipe is restricted to the current OS user. The client, the command line and the MCP
server are three doors to the same operations; none adds one of its own or
reshapes an answer. See [protocol.md](protocol.md) for what goes on the wire, [adapters.md](adapters.md)
for what answers for which control and [cli.md](cli.md) for the command line.

## Wire decisions

**Whole numbers and decimals travel as JSON strings.** Most readers of the protocol - a command line, an
agent, a browser - parse JSON numbers as doubles, which loses both the range of a counter and the
precision of a decimal. `UiInt64Converter` and `UiDecimalConverter` write invariant strings and accept
either form on the way in.

**Moments are UTC.** `UiDateTimeConverter` writes ISO 8601 with a `Z`.

**Answers compare by their wire form.** Records holding an `ImmutableArray<T>` do not compare its
contents, so two snapshots differing only in their rows compare equal; tests compare the serialised form,
which is what the protocol actually promises.
