[CmdletBinding()]
param(
    [ValidateSet('Headless', 'Desktop')]
    [string] $Suite = 'Headless',
    [string] $Configuration = 'Release',
    [string] $ResultsDirectory = 'artifacts/test-results'
)

$ErrorActionPreference = 'Stop'
$ResultsDirectory = [System.IO.Path]::GetFullPath($ResultsDirectory)

if ($Suite -eq 'Headless') {
    $projects = @(
        'DesktopDriver.Contracts.Tests',
        'DesktopDriver.Runtime.Tests',
        'DesktopDriver.Protocol.Tests',
        'DesktopDriver.Cli.Tests',
        'DesktopDriver.Avalonia.Tests',
        'DesktopDriver.Maui.Tests'
    )
    $groups = @($projects | ForEach-Object {
        @{ Project = $_; Filter = 'FullyQualifiedName~StockSharp.DesktopDriver.Tests' }
    })
    $groups += @{ Project = 'DesktopDriver.Mcp.Tests'; Filter = 'FullyQualifiedName~StockSharp.DesktopDriver.Tests.Mcp.ServerSurfaceTests' }
} else {
    $groups = @(
        @{ Project = 'DesktopDriver.Wpf.Tests'; Filter = 'FullyQualifiedName~StockSharp.DesktopDriver.Tests.Wpf' },
        @{ Project = 'DesktopDriver.Mcp.Tests'; Filter = 'FullyQualifiedName~StockSharp.DesktopDriver.Tests.Mcp' }
    )
}

# Test assemblies run in sequence, so process startup does not compete with another suite.
foreach ($group in $groups) {
    $report = 'trx;LogFileName=' + $group.Project + '.trx'
    dotnet test $group.Project --configuration $Configuration --no-build --no-restore `
        --filter $group.Filter --blame-hang-timeout 120s `
        --logger $report --results-directory $ResultsDirectory `
        -- RunConfiguration.TestSessionTimeout=300000

    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
