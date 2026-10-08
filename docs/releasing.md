# Building and releasing packages

The [build workflow](../.github/workflows/build.yml) runs on pushes to `master` and pull requests. It
builds the whole solution on Windows with .NET 10.0.400 and the MAUI Windows workload, runs the tests
that need no desktop session, and uploads the `.nupkg` files and test reports as Actions artifacts.
Tests always have a filter, a two-minute hang timeout and a five-minute session timeout. Assemblies
run in sequence.

`global.json` pins the SDK so an additional SDK on a runner does not change the build. Update it when
changing the SDK and check the resulting build and test run.

`Contracts`, `Runtime` and `Cli` contain unit tests. `Protocol` exercises a real host and client with
Avalonia Headless. The Avalonia suite uses headless controls, and the MAUI suite exercises controls
without their native platform handlers. The MCP surface tests start the server without opening an
application window.

The WPF screenshot tests, MCP application tests and WPF/MAUI Windows integration tests open real windows.
The [samples](samples.md) provide independent applications for all three toolkits. To include desktop tests in CI, register
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
between DesktopDriver packages; tests and the sample applications are not packed. Package identities,
versions, internal dependencies and repository metadata are checked before uploading or publishing.

To publish version `1.0.0`, push the tag on the commit to release:

```sh
git tag v1.0.0
git push origin v1.0.0
```

Alternatively, open **Actions → release → Run workflow**, enter a version without `v`, and turn off
`dry_run` to publish. The manual run creates the tag on the selected commit after the build passes.
Keep `dry_run` enabled to check a release build and the configured trusted publishing policy without
publishing packages or creating a tag or release. A version with a prerelease suffix, such as
`1.0.0-beta.1`, creates a GitHub prerelease.

The publish job uploads all 17 packages to NuGet.org, then attaches them to a GitHub release. GitHub's
automatic `GITHUB_TOKEN` has write access to repository contents only for that job. A GitHub release
version that already exists is refused rather than replaced. NuGet uploads skip existing package
versions so a retry can finish an interrupted batch without replacing published packages.

## NuGet.org

The workflow supports [NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).
Configure it once before a real release:

1. Sign in to the NuGet.org profile that can publish StockSharp packages and open **Trusted Publishing**.
2. Add a policy with repository owner `StockSharp`, repository `DesktopDriver.Mcp` and workflow file
   `release.yml`. Leave the environment empty; this workflow does not use a GitHub environment.
3. Select the package owner and allow new packages and new versions matching `StockSharp.DesktopDriver.*`.
4. Set the GitHub repository Actions variable `NUGET_USER` to that NuGet profile's username, not an email
   or an organization name. The `NuGet/login@v1` action exchanges the job's GitHub OIDC identity for a
   short-lived publishing key. The publish job and the dry-run authentication check have `id-token: write`.

An existing API-key setup can instead provide the GitHub Actions secret `NUGET_API_KEY`, scoped to
publishing `StockSharp.DesktopDriver.*`. This secret takes precedence over trusted publishing.
Keep publishing keys in GitHub Secrets. They are unrelated to the application's local automation
channel and are never passed to the samples, hosts or clients.

A real release checks that one of these configurations is present before building. NuGet.org must
also accept the credentials or policy when publishing; a GitHub push credential or `GITHUB_TOKEN`
does not grant that access. `dry_run` validates the complete package set without uploading it. If
`NUGET_USER` is set, it also verifies the trusted publishing policy through `NuGet/login@v1`. Without
that variable, the authentication check is skipped and no NuGet credentials are needed for a dry run.
