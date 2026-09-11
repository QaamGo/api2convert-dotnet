# Changelog

All notable changes to this package are documented here. This project adheres to
[Semantic Versioning](https://semver.org/).

## [10.4.0] - 2026-09-11

- The package now multi-targets `net8.0` and `net10.0`. Until now only a `net8.0` assembly was
  published, so .NET 10 consumers resolved the `net8.0` build through compatibility fallback; they
  now get a native `net10.0` assembly. `net8.0` remains the supported floor, so this is additive and
  no consumer needs to change anything.
- CI builds and runs the offline unit and security suites against both target frameworks.
- Added a dependency audit gate and enabled Dependabot version updates, and refreshed the test
  toolchain (Microsoft.NET.Test.Sdk 18.10.0, xunit.runner.visualstudio 4.0.0, JunitXml.TestLogger
  8.0.0, actions/checkout 7, actions/setup-dotnet 6).
- Release safety: publishing now refuses to run when the git tag does not match the `<Version>` in
  the csproj, a test project that discovers zero tests fails the build instead of passing silently,
  and the live-conformance job fires on `main` rather than a branch name that did not exist.

## [10.3.1] - 2026-07-12

- Documentation-only release: added a cloud-storage example to the README. No functional or API
  changes.

## [10.3.0] - 2026-07-12

- Added typed cloud-storage connectors: `CloudInput` for sourcing an input from a cloud provider and
  `OutputTarget` for delivering results to one.
- Requests now send the on-brand `x-api2convert-*` header names.
- Hardened the transport and downloads: the control-plane response-body read is bounded, upload
  streaming honors a timeout, downloads save atomically, error typing is more precise, and numeric
  overflow is handled.
- Documentation: corrected the stats filter reference (the filter is `single` or `all`, never a key
  in the URL) and added CI, version, language and license badges to the README.

## [10.2.1] - 2026-07-08

- First public release of the official API2Convert .NET SDK (`Api2Convert`) on NuGet, at feature
  parity with the sibling SDKs and versioned in lock-step at 10.2.1.
- Includes the hardened HTTP transport and downloads: secret-bearing requests never follow redirects
  (no cross-host leak of `X-Api2convert-Api-Key` / `X-Api2convert-Token` / `X-Api2convert-Download-Password`), partial downloads
  are cleaned up on error, URL path segments are percent-encoded, and an empty API key throws a typed
  `ConfigurationException`.
