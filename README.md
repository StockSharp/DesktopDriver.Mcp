# StockSharp DesktopDriver

Drive desktop applications built on Avalonia, WPF or MAUI from tests, from a command line, or from an AI
agent over MCP.

DesktopDriver reads the interface, not the screen. A table answers with its columns and rows, a tree with
its items, a workspace with its panels - in words that do not change with a theme, a font or a language.
Input goes through the toolkit's own pipeline, and only into a run that cannot reach anything real.
Pictures are there for a person to look at; tests check what the controls say.

## How it works

1. An application that can be driven references the bootstrap of its toolkit. Started the ordinary way it
   opens nothing. Started with `--ui-automation`, it opens a local named-pipe endpoint restricted to the
   current OS user, and writes where the endpoint is to the file named by `--ui-automation-endpoint=<path>`.
2. A runner - a test, the command line, the MCP server - starts the application that way (or attaches to
   one somebody else started), checks the product, instance and protocol in the session handshake and asks:
   which windows are open, what is in this one, what does this table show. It can then click, type, press
   keys and scroll, and wait for the answer to change.
3. Adapters turn controls into what the protocol knows: grids, trees, lists, pickers, workspaces, charts,
   documents, diagrams. An application registers adapters for its own controls through the same
   interface the standard ones use.

## Packages

| Package | What it is |
|---|---|
| `StockSharp.DesktopDriver.Contracts` | The protocol: requests, answers, states, addresses |
| `StockSharp.DesktopDriver.Runtime` | Reading, identity, revisions and waiting, independent of any toolkit |
| `StockSharp.DesktopDriver.Host` | The endpoint inside the application, and `UiAutomationLaunch` to read how it was started |
| `StockSharp.DesktopDriver.Client` | The typed client every runner talks through |
| `StockSharp.DesktopDriver.Runner` | Starts, finds and reaches applications; the catalogue of what may be started |
| `StockSharp.DesktopDriver.Testing` | What an MSTest suite needs on top of the runner |
| `StockSharp.DesktopDriver.Cli` | The command line |
| `StockSharp.DesktopDriver.Mcp` | The MCP server, see [its README](DesktopDriver.Mcp/README.md) |
| `StockSharp.DesktopDriver.Avalonia` | Avalonia: reading, input and pictures of the standard controls |
| `StockSharp.DesktopDriver.Avalonia.ProDataGrid` | Avalonia: the `DataGrid` from ProDataGrid, read as a table |
| `StockSharp.DesktopDriver.Avalonia.Dock` | Avalonia: a Dock.Avalonia workspace, read as panels and groups |
| `StockSharp.DesktopDriver.Avalonia.Headless` | Avalonia: input for headless tests |
| `StockSharp.DesktopDriver.Bootstrap.Avalonia` | Avalonia: one call that makes an application drivable |
| `StockSharp.DesktopDriver.Wpf` | WPF: reading, input and pictures of the standard controls |
| `StockSharp.DesktopDriver.Bootstrap.Wpf` | WPF: one call that makes an application drivable |
| `StockSharp.DesktopDriver.Maui` | MAUI: reading, input and pictures of the standard controls |
| `StockSharp.DesktopDriver.Bootstrap.Maui` | MAUI on Windows: one call that makes an application drivable |

## Make an application drivable

Avalonia, once the main window exists ([the sample](Samples/DesktopDriver.Sample.Avalonia) does the same with the
grid module alone):

```csharp
public override void OnFrameworkInitializationCompleted()
{
	if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
	{
		desktop.MainWindow = new MainWindow();

		if (UiAutomationLaunch.TryRead(desktop.Args) is { } launch)
		{
			_automation = AvaloniaAutomationBootstrap.Start(
				UiAutomationStartup.For("my.app", version, fixtureId: null, hasLiveOutsideWorld: false, isTestProfile: false),
				launch.EndpointFile,
				[binder => new DataGridAutomationModule(binder), binder => new DockAutomationModule(binder)]);
		}
	}

	base.OnFrameworkInitializationCompleted();
}
```

Dispose the returned runtime when the application exits. `WpfAutomationBootstrap` and
`MauiAutomationBootstrap` take the same arguments.

`UiAutomationStartup.For` decides whether input is allowed. An application that talks to nothing outside
its own process is always safe to click through. One that connects to something real - a broker, a
store, a service - is safe only when it was started on a test profile that replaces it; otherwise the
endpoint still reads, but every input is refused.

Many applications ship two builds: an ordinary one with no driver in it, and one made to be driven. The
driver is only referenced by the second, so the first has nothing listening by construction.

## Drive it

From a test:

```csharp
await using var app = await DrivenApplication.StartAsync(executable, extraArguments: null, TimeSpan.FromSeconds(60), cancellationToken);
await using var client = await app.ConnectAsync("my.app", cancellationToken);

var windows = await client.GetSurfacesAsync(cancellationToken);
```

From a command line, against an application somebody started with `--ui-automation`:

```sh
dotnet tool install StockSharp.DesktopDriver.Cli --tool-path tools
tools/desktop-driver find --endpoint endpoint.json --kind grid
tools/desktop-driver grid-rows --endpoint endpoint.json --node window:MainWindow/Instruments --limit 5
```

`desktop-driver` with no arguments prints every operation.

From an agent: register the MCP server, see [DesktopDriver.Mcp](DesktopDriver.Mcp/README.md).

## Documents

- [How the driver is built](docs/architecture.md)
- [The protocol](docs/protocol.md)
- [What answers for which control](docs/adapters.md), and how to add an adapter
- [The command line](docs/cli.md)
- [The MCP server](DesktopDriver.Mcp/README.md)

## Build and test

```sh
dotnet build DesktopDriver.slnx
dotnet test DesktopDriver.Runtime.Tests --filter "FullyQualifiedName~Revision"
```

The MAUI projects need the MAUI workload. The MCP tests start the sample and the WPF tests show a window, so
both need a desktop session.

## Packages from source

```sh
dotnet pack DesktopDriver.slnx -c Release -o artifacts/packages
```

## Sources instead of packages

A product checked out beside this repository can reference the projects themselves:

```xml
<ProjectReference Include="..\DesktopDriver.Mcp\DesktopDriver.Bootstrap.Avalonia\DesktopDriver.Bootstrap.Avalonia.csproj" />
```

A product that builds a second, drivable copy of itself by passing a property on the command line adds
`GlobalPropertiesToRemove="<that property>"` to the reference: the driver has no build of its own for it, and
would otherwise be built twice into the same folder.

## License

See [LICENSE](LICENSE).
