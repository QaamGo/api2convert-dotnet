# Security Policy

## Reporting a vulnerability

Please **do not** open a public GitHub issue for a security problem in this SDK.

Report it privately through GitHub's **"Report a vulnerability"** button under the repository's
*Security* tab (private vulnerability reporting). If that is unavailable, use the support channels at
<https://www.api2convert.com>. Please avoid disclosing details publicly until a fix has been released.

## Secrets this SDK handles

The library handles several secrets on the caller's behalf — keep all of them out of source control
and configure them via environment variables or a secret manager:

- the **account API key** (`X-Oc-Api-Key`) — read from configuration/environment
  (`API2CONVERT_API_KEY`) and sent only over TLS to the API host, never in a URL query string;
- the **per-job upload token** (`X-Oc-Token`) — used to authenticate uploads to the per-job upload
  server; the account key is **never** sent there;
- the **download password** (`X-Oc-Download-Password`) — sent only to fetch a protected output;
- the **webhook signing secret** — used locally to verify callback signatures (HMAC-SHA256 over the
  raw request body, constant-time comparison via `CryptographicOperations.FixedTimeEquals`). The
  signature is delivered in the `X-Oc-Signature` header.

## Guarantees

Each of these is proven by the independent security suite in
[`test/Api2Convert.SecurityTests`](test/Api2Convert.SecurityTests) (run it with
`make test-security`), which uses **real loopback HTTP servers** for the redirect guarantees.

- The SDK never logs a key/token and never places one in an exception message or a URL/query string.
- A request that carries **any secret in a custom `X-Oc-*` header never follows an HTTP redirect** — a
  redirect could otherwise forward the secret to another host (.NET's `HttpClient` strips
  `Authorization` on a cross-host redirect, but not arbitrary `X-Oc-*` headers). This covers the
  account key (`X-Oc-Api-Key`), the per-job upload token (`X-Oc-Token`) **and** a download password
  (`X-Oc-Download-Password`). Only a plain, passwordless download (`GET output.Uri`, which carries no
  secret) follows redirects, so storage/CDN URLs still resolve. This is enforced with two
  `HttpClientHandler`s (one with `AllowAutoRedirect = false`, one with it enabled), chosen per request.
- A directory download uses a **sanitized basename** derived from the API-supplied filename, so a
  malicious name (e.g. `../../evil`) cannot escape the target directory; the multipart upload filename
  is likewise sanitized against header injection (CR/LF/quote/NUL stripped).
- Untrusted JSON hydrates **defensively**: a hostile or surprising payload never throws, unknown fields
  are preserved (in `Raw`), and the URL classifier (`^https?://`) is anchored and linear (no ReDoS).
- Transient failures are retried with capped, jittered backoff; a non-idempotent `POST` is never
  blindly retried, so a transient error cannot create a duplicate job.
- Webhook signatures are verified in constant time; a tampered payload, a wrong (equal-length)
  signature or a missing signature is rejected.

If a key is ever exposed, revoke and rotate it in the API2Convert dashboard immediately.
