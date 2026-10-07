# Sample applications

Each sample opens its automation endpoint only when started with `--ui-automation`. All data and
actions stay inside the process, so input is safe without an external test profile. The bootstrap
receives `hasLiveOutsideWorld: false`; an application that connects to real services must report
that accurately and use an isolated test profile before allowing input.

| Application | Integration | Tests |
|---|---|---|
| [Avalonia](../Samples/DesktopDriver.Sample.Avalonia) | Application startup and shutdown; ProDataGrid module | MCP server starts and attaches to it, reads tables and retrieves pictures |
| [WPF](../Samples/DesktopDriver.Sample.Wpf) | `Application.OnStartup` and `OnExit` | Windows integration tests read a list, type into a text box, click a button and a check box, and retrieve control and window pictures |
| [MAUI Windows](../Samples/DesktopDriver.Sample.Maui.Windows) | `Window.Created` and `Destroying`; real WinUI handlers | The same reading and input scenarios; native control rendering and refusal of unsupported window capture |

The Windows samples expose `InputText`, `ApplyButton`, `ResultText`, `EnabledToggle` and `ItemList`
under `window:MainWindow`. Applying text changes the result label to `Applied: <text>`. The list
contains three local items. These controls exercise the standard toolkit adapters without product
adapters or external connections.

## Start a sample

Build the solution with the SDK in `global.json` and the MAUI Windows workload:

```powershell
dotnet build DesktopDriver.slnx --configuration Release
```

WPF:

```powershell
./Samples/DesktopDriver.Sample.Wpf/bin/Release/net10.0-windows/StockSharp.DesktopDriver.Sample.Wpf.exe --ui-automation --ui-automation-endpoint=endpoint-wpf.json
```

MAUI Windows:

```powershell
./Samples/DesktopDriver.Sample.Maui.Windows/bin/Release/net10.0-windows10.0.19041.0/win-x64/StockSharp.DesktopDriver.Sample.Maui.Windows.exe --ui-automation --ui-automation-endpoint=endpoint-maui.json
```

The MAUI sample is an unpackaged Windows x64 application. Its build output includes the Windows App
SDK runtime, so it runs directly without an MSIX installation. It still needs the .NET 10 runtime.
Both samples can also be launched normally; an endpoint-file argument alone does not enable automation.

## Run the Windows integration tests

`DesktopDriver.Windows.Tests` starts a separate application for every scenario. Its typed client
speaks the actual named-pipe protocol; tests verify the product and instance, stable addresses,
native layout, control values after input, PNG artifacts and screenshot kinds. Startup without the
enable switch is tested separately. Each test closes only the process it started.

After a Release build, in an interactive Windows desktop session:

```powershell
dotnet test DesktopDriver.Windows.Tests --configuration Release --no-build --filter "FullyQualifiedName~SampleApplicationTests" --blame-hang-timeout 120s -- RunConfiguration.TestSessionTimeout=300000
```

The [Desktop test script](../.github/scripts/Test.ps1) includes these tests alongside the WPF screenshot
and MCP application suites. `DesktopDriver.Maui.Tests` remains the suite for names, state, revisions,
gestures and keys without native handlers; the Windows suite exercises the actual platform backend.
