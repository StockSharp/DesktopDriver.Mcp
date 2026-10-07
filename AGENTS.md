# AGENTS.md - DesktopDriver

> Workspace-wide rules (English in code and commits, Russian in chat, TDD, never auto-push, C# style) live in
> the configs repository and load automatically. This file covers what is specific to this repository.

## What this is

StockSharp DesktopDriver reads and drives desktop applications built on Avalonia, WPF or MAUI - from tests, from
a command line, from an AI agent over MCP. It knows nothing about any StockSharp product: the adapters for the
products' own controls live with the products (`stocksharpapps/UiAutomation`), and register through the same
interface the standard ones here use.

`README.md` is the description for a user of the driver; `docs/` holds the architecture, the protocol, the
adapters and the command line.

## Layout

| Projects | What they hold |
|---|---|
| `DesktopDriver.Contracts` | the protocol: requests, answers, states, addresses |
| `DesktopDriver.Runtime` | reading, identity, revisions, waiting and the input dispatcher, independent of any toolkit |
| `DesktopDriver.Host`, `DesktopDriver.Client` | the endpoint inside an application and the typed client that talks to it |
| `DesktopDriver.Runner`, `DesktopDriver.Testing` | starting and finding applications; what an MSTest suite needs on top |
| `DesktopDriver.Cli`, `DesktopDriver.Mcp` | the command line (`desktop-driver`) and the MCP server (`desktop-driver-mcp`) |
| `DesktopDriver.Avalonia*`, `DesktopDriver.Wpf`, `DesktopDriver.Maui` | one backend per toolkit: reading, input and pictures of its standard controls |
| `DesktopDriver.Bootstrap.*` | one call that makes an application of that toolkit drivable |
| `Samples/DesktopDriver.Sample.Avalonia` | the application the MCP tests drive |
| `*.Tests` | a test project per layer |

## Build and test

```sh
dotnet build DesktopDriver.slnx
dotnet test DesktopDriver.Runtime.Tests --filter "FullyQualifiedName~Revision"
```

Always run tests with a `--filter`. `DesktopDriver.Mcp.Tests` start the sample and `DesktopDriver.Wpf.Tests` show
a window, so both need a Windows desktop session; the MAUI projects need the MAUI workload.

Build settings are in `Directory.Build.props` and nowhere else: the assembly of a project is
`StockSharp.<project name>`, a project whose name ends in `.Tests` is a test project, and nothing is imported
from the core's props.

## Who builds against it

The applications (`stocksharpapps`) and the Shell (`EduGit`) reference these projects from source through
`$(DesktopDriverPath)`, so this repository is checked out beside them as `DesktopDriver.Mcp`. A change to a
public type here is a change to them: after one, build `stocksharpapps/StockSharp_UiAutomation.slnx` and an
instrumented application (`-p:EnableUiAutomation=true`) before committing.

Packages are made with `dotnet pack DesktopDriver.slnx -c Release -o artifacts/packages`; `artifacts/` is not
committed.

## Do not break

- **Input is refused unless the run cannot reach anything real.** `UiAutomationStartup.For` decides it and
  `UiInputDispatcher` checks the refusal before a control is resolved or offered the action. A control that
  performs its own input is not a way round it.
- **A picture says which kind it is.** A control drawing itself and a window photographed from the desktop are
  different evidence; a backend that cannot produce the kind asked for refuses rather than answers with the other.
- **Addresses are stable.** A node is `scope/id`; an id is the automation id or the name the markup gave. A path
  derived from where the control sits (it starts with `~`) is only the fallback for a control that has neither.
