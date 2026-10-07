# Building and releasing packages

The [build workflow](../.github/workflows/build.yml) runs on pushes to `master` and pull requests. It
builds the whole solution on Windows with .NET 10.0.400 and the MAUI Windows workload, runs the tests
that need no desktop session, and uploads the `.nupkg` files and test reports as Actions artifacts.
Tests always have a filter, a two-minute hang timeout and a five-minute session timeout. Assemblies
run in sequence.

`Contracts`, `Runtime` and `Cli` contain unit tests. `Protocol` exercises a real host and client with
Avalonia Headless. The Avalonia suite uses headless controls, and the MAUI suite exercises controls
without their native platform handlers. The MCP surface tests start the server without opening an
application window.

The WPF screenshot tests and MCP application tests open real windows. To include these in CI, register
a Windows x64 self-hosted runner with the `desktop` label, start it in an interactive desktop session,
and set the repository variable `DESKTOP_TESTS_ENABLED` to `true`. The runner needs .NET 10.0.400 and
the MAUI Windows workload. This job runs on trusted branch pushes and release runs; pull requests
use the hosted runner. Without that configuration, desktop tests can be run locally after a Release
build:

```powershell
./.github/scripts/Test.ps1 -Suite Desktop
```

## GitHub releases

The [release workflow](../.github/workflows/release.yml) follows the tag or manual release model used
by [StockSharp/Odysseus](https://github.com/StockSharp/Odysseus/blob/main/.github/workflows/release.yml).
For DesktopDriver, the assets are NuGet library and .NET tool packages. A release rebuilds, tests and
packs the tagged source before publishing. All packages use the same version, including dependencies
between DesktopDriver packages; tests and the sample application are not packed.

To publish version `1.0.0`, push the tag on the commit to release:

```sh
git tag v1.0.0
git push origin v1.0.0
```

Alternatively, open **Actions → release → Run workflow**, enter a version without `v`, and turn off
`dry_run` to publish. The manual run creates the tag on the selected commit after the build passes.
Keep `dry_run` enabled to check a release build without creating a tag or release. A version with a
prerelease suffix, such as `1.0.0-beta.1`, creates a GitHub prerelease.

The workflow uses GitHub's automatic `GITHUB_TOKEN` with write access to repository contents only
for the publish job. No separate secret is needed for a GitHub release. It publishes `.nupkg` assets
to GitHub Releases; it does not upload them to NuGet.org. A release version that already exists is
refused rather than replaced.

## NuGet.org

Publishing to NuGet.org is a separate configuration. It can use
[NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing):
register this repository and the publishing workflow with the NuGet package owner, then exchange a
GitHub Actions identity for a short-lived publishing key with `NuGet/login`. A GitHub push credential
or `GITHUB_TOKEN` does not grant publishing access to NuGet.org.
