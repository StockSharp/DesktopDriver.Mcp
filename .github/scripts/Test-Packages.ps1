[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $PackageDirectory,
    [Parameter(Mandatory)]
    [string] $Version
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repositoryDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
[xml] $solution = Get-Content -LiteralPath (Join-Path $repositoryDirectory 'DesktopDriver.slnx') -Raw
$expectedIds = @($solution.SelectNodes('//Project') | ForEach-Object {
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($_.Path)
    if (-not $projectName.EndsWith('.Tests') -and -not $projectName.StartsWith('DesktopDriver.Sample.')) {
        'StockSharp.' + $projectName
    }
})
$packages = @(Get-ChildItem -LiteralPath $PackageDirectory -Filter '*.nupkg' -File)
if ($packages.Count -ne $expectedIds.Count) {
    throw "Expected $($expectedIds.Count) packages, found $($packages.Count)."
}

$actualIds = @()
foreach ($package in $packages) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $specifications = @($archive.Entries | Where-Object FullName -Like '*.nuspec')
        if ($specifications.Count -ne 1) { throw "$($package.Name) must contain exactly one nuspec." }
        $reader = [System.IO.StreamReader]::new($specifications[0].Open())
        try { [xml] $specification = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $specification.package.metadata
        $actualIds += [string] $metadata.id
        if ($metadata.version -ne $Version) {
            throw "$($package.Name) has version $($metadata.version), expected $Version."
        }
        if ($metadata.repository.url -ne 'https://github.com/StockSharp/DesktopDriver.Mcp') {
            throw "$($package.Name) does not identify this source repository."
        }
        foreach ($dependency in $specification.SelectNodes('//*[local-name()="dependency"]')) {
            if ($expectedIds -contains $dependency.id -and $dependency.version -ne $Version -and $dependency.version -ne "[$Version, )") {
                throw "$($package.Name) depends on $($dependency.id) $($dependency.version), expected $Version."
            }
        }
    } finally {
        $archive.Dispose()
    }
}

if (@(Compare-Object ($expectedIds | Sort-Object) ($actualIds | Sort-Object)).Count -ne 0) {
    throw 'The packages do not match the production projects in the solution.'
}
Write-Output "Validated $($packages.Count) DesktopDriver packages at version $Version."
