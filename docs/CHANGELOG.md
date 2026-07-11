# Changelog

All notable changes to this package are documented here. This project adheres to
[Semantic Versioning](https://semver.org/).

## [10.2.1] - 2026-07-08

- First public release of the official API2Convert .NET SDK (`Api2Convert`) on NuGet, at feature
  parity with the sibling SDKs and versioned in lock-step at 10.2.1.
- Includes the hardened HTTP transport and downloads: secret-bearing requests never follow redirects
  (no cross-host leak of `X-Api2convert-Api-Key` / `X-Api2convert-Token` / `X-Api2convert-Download-Password`), partial downloads
  are cleaned up on error, URL path segments are percent-encoded, and an empty API key throws a typed
  `ConfigurationException`.
