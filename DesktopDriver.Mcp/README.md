# StockSharp.DesktopDriver.Mcp

[![MCP on NuGet](https://img.shields.io/nuget/v/StockSharp.DesktopDriver.Mcp?label=MCP)](https://www.nuget.org/packages/StockSharp.DesktopDriver.Mcp)

An MCP server that lets an agent read and drive desktop applications. It speaks MCP on stdio, so a
client spawns it as a child process; it talks to the applications over the named-pipe protocol in
`StockSharp.DesktopDriver.Contracts`, the same one the command line and the tests use.

It reads the interface rather than the screen. A grid answers with its rows and their values, a chart
with the points it drew, an order book with its levels - in words that do not change with a theme, a
font or a language. Pictures are for a person to look at, not for a test to measure.

## Install from NuGet

Install [StockSharp.DesktopDriver.Mcp](https://www.nuget.org/packages/StockSharp.DesktopDriver.Mcp)
as a .NET tool, which puts `desktop-driver-mcp` in the folder named:

```sh
dotnet tool install StockSharp.DesktopDriver.Mcp --tool-path tools
```

The server writes the protocol to stdout and every log line to stderr. Running it by hand is only
useful with a client attached; on its own it waits for stdin.

## Build from source

```sh
dotnet build DesktopDriver.Mcp/DesktopDriver.Mcp.csproj -c Release
```

## Set it up

Two things, both from the environment and neither from the agent:

| variable | what it is |
|---|---|
| `STOCKSHARP_UI_CATALOGUE` | the file listing which applications may be started; `applications.json` beside the server when unset |
| `STOCKSHARP_UI_ARTIFACTS` | where pictures are written; an `artifacts` folder beside the server when unset |

Installed as a tool, the server lives in the tool store, so "beside the server" is inside it: name both in
the environment instead.

`applications.sample.json` in this folder is a catalogue to copy and edit; its relative paths are
resolved against the catalogue file itself, so a copy kept beside it works from anywhere. Every
application in it has to be a build with the driver in it. A build without one has no endpoint to
connect to, and the server says so.

The catalogue is the whole list of what an agent may start. It names applications, not paths: a server
that started whatever path it was handed would be a way to run anything on the machine.

## Register it with a client

Claude Code, in `.mcp.json`:

```json
{
  "mcpServers": {
    "desktop-driver": {
      "command": "DesktopDriver.Mcp/bin/Release/net10.0/StockSharp.DesktopDriver.Mcp.exe",
      "env": {
        "STOCKSHARP_UI_CATALOGUE": "DesktopDriver.Mcp/applications.json"
      }
    }
  }
}
```

Codex, in `~/.codex/config.toml`:

```toml
[mcp_servers.desktop-driver]
command = "DesktopDriver.Mcp/bin/Release/net10.0/StockSharp.DesktopDriver.Mcp.exe"
env = { STOCKSHARP_UI_CATALOGUE = "DesktopDriver.Mcp/applications.json" }
```

The server starts applications with `--ui-automation`. To reach an application somebody else started
that way, call `ui_attach_application` with its `endpointFile`. The local named pipe is restricted to
the current OS user; the session handshake checks the product, running copy and protocol version.

## Tools

| Tool | What it does |
|---|---|
| `ui_list_applications` | What may be started, what is built and what is already running |
| `ui_start_application` | Starts one and waits until its window is drawn |
| `ui_attach_application` | Connects to one somebody else started |
| `ui_release_application` | Closes one this server started; lets go of one it only attached to |
| `ui_session` | What answered and what it can do |
| `ui_windows` | The windows and popups on screen |
| `ui_find` | Finds nodes by name, kind, automation id or text |
| `ui_tree` | The tree of nodes, bounded by depth and count |
| `ui_snapshot` | One node's whole state |
| `ui_grid_columns` / `ui_grid_rows` / `ui_grid_groups` | A table, as the table itself has it |
| `ui_chart_series` / `ui_chart_points` | What a chart actually drew |
| `ui_order_book_levels` | A book's levels, keyed by side and price |
| `ui_property_items` | What a property editor is showing |
| `ui_dock_layout` | How a workspace is arranged |
| `ui_tree_items` | What a tree is showing, by path |
| `ui_document_content` | The lines of a code editor or a markdown viewer |
| `ui_diagram_nodes` / `ui_diagram_connections` | The blocks a diagram drew and what it joined |
| `ui_click` / `ui_type_text` / `ui_press_key` / `ui_scroll` | Real input on the machine's own desktop |
| `ui_action_status` | What became of an input already sent |
| `ui_wait` | Waits for a node or one of its fields |
| `ui_screenshot` / `ui_screenshot_image` | A picture, as a file or as an image |
| `ui_diagnostics` | What the module itself has been saying |

A chart, an order book, a property editor, a document or a diagram is read when the application
registered an adapter for that control; the standard controls of each toolkit, an Avalonia `DataGrid`
and a Dock workspace are read by the packages of the driver itself.

Addresses come from the listing tools: `ui_windows` or `ui_find` first, then act on what they named. An
address is `scope/identifier` and survives the panel being moved, floated out or rebuilt.

## What it can actually do to you

`ui_click`, `ui_type_text`, `ui_press_key` and `ui_scroll` send **real** input to the machine's own
desktop - the same clicks and keystrokes a person would send, to whatever is under them. Against an
application connected to a live account, a click on a button that places an order places one; that is
why an application that reaches anything real refuses input unless it was started on a test profile.

`ui_release_application` closes an application this server started, without asking it to save anything.

Everything else only reads.

## What "it worked" does not mean

An input coming back `dispatched` means the click was delivered, and nothing more. Whether the button
did what was hoped is a separate question, asked with `ui_snapshot` or `ui_wait`. The two are kept
apart on purpose: a click that arrived at a disabled button arrived.

Input needs a desktop. On a machine with no session attached - a service, a locked console, a build
agent - the refusal says `inputBlocked`, and that is the machine rather than the application.
