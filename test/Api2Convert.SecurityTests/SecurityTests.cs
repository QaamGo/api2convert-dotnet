using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Api2Convert;
using Api2Convert.Exceptions;
using Api2Convert.Http;
using Api2Convert.Models;
using Xunit;

namespace Api2Convert.SecurityTests;

/// <summary>
/// The independent security suite. Black-box: it uses only the public Api2Convert surface. The
/// redirect guarantees are proven with REAL loopback servers — a secret in a custom <c>X-Oc-*</c>
/// header must never be forwarded to a redirect target. Header/JSON/classifier checks use the public
/// <see cref="IHttpSender"/> seam, where a real round-trip adds nothing.
/// </summary>
public sealed class SecurityTests
{
    private static Config NoRetry(string? baseUrl = null)
    {
        var builder = new Config.Builder().MaxRetries(0);
        if (baseUrl is not null)
        {
            builder.BaseUrl(baseUrl);
        }

        return builder.Build();
    }

    // -------------------- streaming timeout (H2) --------------------

    [Fact]
    public async Task ASlowUploadIsNotCappedByThePerRequestTimeout()
    {
        // A streamed upload transmits its whole body inside SendAsync, so a whole-request timeout would
        // abort a large/slow upload. The server delays its response well past the (floored 1s) timeout;
        // the upload must still succeed because a streamed transfer is bounded only by the caller.
        using var server = LoopbackServer.RespondingAfterDelay(
            200, "{\"id\":\"in-1\",\"type\":\"upload\"}", TimeSpan.FromMilliseconds(1500));
        var config = new Config.Builder().MaxRetries(0).Timeout(1).Build();
        using var client = new Api2ConvertClient("k", config);

        var job = Job.FromDict(new Dictionary<string, object?>
        {
            ["id"] = "job-9",
            ["token"] = "tok-abc",
            ["server"] = server.BaseUrl,
            ["status"] = new Dictionary<string, object?> { ["code"] = "incomplete" },
        });

        InputFile input = await client.Jobs.UploadAsync(job, Encoding.UTF8.GetBytes("hello world"));
        Assert.Equal("in-1", input.Id);
    }

    // -------------------- secret hygiene --------------------

    [Fact]
    public async Task SecretNeverAppearsInAnExceptionButIsSentAsTheAuthHeader()
    {
        const string secret = "sk_live_super_secret_value_123";
        using var api = LoopbackServer.Responding(401, "{\"message\":\"Invalid API key.\"}");
        using var client = new Api2ConvertClient(secret, NoRetry(api.BaseUrl + "/v2"));

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => client.Jobs.GetAsync("job-x"));

        Assert.DoesNotContain(secret, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, ex.ToString(), StringComparison.Ordinal);
        // ...but it WAS sent as the auth header (the request was genuinely authenticated).
        Assert.Equal(secret, api.HeadersReceived[0]["X-Oc-Api-Key"]);
    }

    [Fact]
    public async Task ApiKeyNeverAppearsInTheUrlOrQueryString()
    {
        const string key = "sk_live_in_url_check";
        var http = new StubSender().Json(200, "[]").Json(200, "[]");
        using var client = new Api2ConvertClient(key, Config.Default, http);

        await client.OptionsAsync("jpg", "image");
        await client.Jobs.ListAsync("completed", 2);

        var keyInQuery = new Regex("[?&](api[-_]?key|apikey|key)=", RegexOptions.IgnoreCase);
        foreach (HttpRequest request in http.Requests)
        {
            Assert.DoesNotContain(key, request.Uri, StringComparison.Ordinal);
            Assert.False(keyInQuery.IsMatch(request.Uri), $"URL carries a key-like query param: {request.Uri}");
        }
    }

    // -------------------- redirect policy (real loopback servers) --------------------

    [Fact]
    public async Task AccountKeyIsNotForwardedAcrossACrossHostRedirect()
    {
        using var evil = LoopbackServer.Responding(200, "grabbed");
        using var api = LoopbackServer.RedirectingTo(evil.BaseUrl + "/steal");
        using var client = new Api2ConvertClient("secret-key", NoRetry(api.BaseUrl + "/v2"));

        // The 302 is not followed (so the key can't reach the redirect target) and is now surfaced
        // as a typed error rather than silently decoding the redirect body into an empty model.
        await Assert.ThrowsAsync<NetworkException>(() => client.Jobs.GetAsync("j"));

        Assert.Equal(0, evil.Hits); // the account key must never reach the redirect target
        Assert.Equal(1, api.Hits);
    }

    [Fact]
    public async Task UploadUsesTheJobTokenNotTheAccountKeyAndDoesNotRedirect()
    {
        using var evil = LoopbackServer.Responding(200, "grabbed");
        using var uploadServer = LoopbackServer.RedirectingTo(evil.BaseUrl + "/steal");
        using var client = new Api2ConvertClient("secret-key", NoRetry());

        Job job = Job.FromDict(new Dictionary<string, object?>
        {
            ["id"] = "job-9",
            ["token"] = "tok-abc",
            ["server"] = uploadServer.BaseUrl,
            ["status"] = new Dictionary<string, object?> { ["code"] = "incomplete" },
        });

        // The upload sends X-Oc-Token (never the account key), does not follow the 302,
        // and surfaces the un-followed redirect as a typed error rather than swallowing it.
        await Assert.ThrowsAsync<NetworkException>(
            () => client.Jobs.UploadAsync(job, Encoding.UTF8.GetBytes("hello")));

        Assert.True(uploadServer.Hits >= 1);
        Assert.Equal("tok-abc", uploadServer.HeadersReceived[0]["X-Oc-Token"]);
        Assert.False(uploadServer.HeadersReceived[0].ContainsKey("X-Oc-Api-Key"));
        Assert.Equal(0, evil.Hits); // an authenticated upload must not follow a redirect
    }

    [Fact]
    public async Task DownloadPasswordIsNotForwardedAcrossACrossHostRedirect()
    {
        using var evil = LoopbackServer.Responding(200, "grabbed");
        using var storage = LoopbackServer.RedirectingTo(evil.BaseUrl + "/steal");
        using var client = new Api2ConvertClient("secret-key", NoRetry());

        var output = OutputFile.Of("o", storage.BaseUrl + "/f.pdf", null);

        // The un-followed 302 must not silently yield an empty "download" — it surfaces as a
        // NetworkException so a corrupt/empty file never lands on disk.
        await Assert.ThrowsAsync<NetworkException>(() => client.Download(output, "s3cret").ContentsAsync());

        Assert.Equal(0, evil.Hits); // the download password must never reach the redirect target
        // ...but it WAS sent to the intended storage host (the request was real).
        Assert.Equal("s3cret", storage.HeadersReceived[0]["X-Oc-Download-Password"]);
    }

    [Fact]
    public async Task PasswordlessDownloadFollowsAStorageRedirect()
    {
        using var storage = LoopbackServer.Responding(200, "REDIRECTED-BYTES");
        using var download = LoopbackServer.RedirectingTo(storage.BaseUrl + "/file");
        using var client = new Api2ConvertClient("secret-key", NoRetry());

        var output = OutputFile.Of("o", download.BaseUrl + "/result.bin", null);
        byte[] bytes = await client.Download(output).ContentsAsync();

        Assert.Equal("REDIRECTED-BYTES", Encoding.UTF8.GetString(bytes));
        Assert.Equal(1, storage.Hits);
    }

    [Fact]
    public async Task MalformedApiSuppliedUriSurfacesAsNetworkException()
    {
        using var client = new Api2ConvertClient("k", NoRetry());
        var output = OutputFile.Of("o", "https://example.com:99999/result.bin", "f.pdf");

        await Assert.ThrowsAsync<NetworkException>(() => client.Download(output).ContentsAsync());
    }

    // -------------------- filesystem safety --------------------

    [Fact]
    public async Task TraversalFilenameIsReducedToBasename()
    {
        var http = new StubSender().Raw(200, Encoding.UTF8.GetBytes("X"));
        using var client = new Api2ConvertClient("k", Config.Default, http);
        DirectoryInfo dir = Directory.CreateTempSubdirectory("a2c-sec");
        try
        {
            var output = OutputFile.Of(null, "https://dl/x", "../../../etc/evil");
            string path = await client.Download(output).SaveAsync(dir.FullName);

            Assert.Equal(Path.Combine(dir.FullName, "evil"), path);
            Assert.False(File.Exists(Path.Combine(dir.FullName, "..", "..", "..", "etc", "evil")));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    // -------------------- webhook signature verification --------------------

    [Fact]
    public void WebhookSignatureVerification()
    {
        const string secret = "whsec_test";
        const string payload = "{\"id\":\"job-1\",\"status\":{\"code\":\"completed\"}}";
        string signature = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        // accepts a valid signature
        var evt = Api2ConvertClient.Webhooks().ConstructEvent(payload, signature, secret);
        Assert.Equal("job-1", evt.Job.Id);

        // rejects a tampered payload
        Assert.Throws<SignatureVerificationException>(() =>
            Api2ConvertClient.Webhooks().ConstructEvent(payload + " ", signature, secret));

        // rejects an equal-length wrong signature (constant-time, no crash)
        Assert.Throws<SignatureVerificationException>(() =>
            Api2ConvertClient.Webhooks().ConstructEvent(payload, new string('f', signature.Length), secret));

        // empty secret is a deliberate verification bypass
        var bypass = Api2ConvertClient.Webhooks().ConstructEvent(payload, signature: null, secret: "");
        Assert.Equal("job-1", bypass.Job.Id);
    }

    // -------------------- untrusted-JSON hardening --------------------

    [Fact]
    public void HostileJsonHydratesWithoutThrowingAndPreservesUnknownFields()
    {
        // .NET has no prototype-pollution analog: a "__proto__"/"constructor" payload is just data.
        // The guarantee is that a hostile/surprising payload hydrates without throwing and unknown
        // fields are tolerated (kept in Raw), never rejected.
        const string malicious =
            "{\"__proto__\":{\"polluted\":true},\"constructor\":{\"prototype\":{\"x\":1}},"
            + "\"id\":\"job-1\",\"surprise_field\":[1,2,3],\"status\":{\"code\":\"completed\"}}";

        var evt = Api2ConvertClient.Webhooks().Parse(malicious);

        Assert.Equal("job-1", evt.Job.Id);
        Assert.True(evt.Job.IsCompleted);
        Assert.True(evt.Job.Raw.ContainsKey("surprise_field"));
    }

    // -------------------- ReDoS / anchored URL classifier --------------------

    [Fact]
    public async Task InputClassifierIsAnchoredAndLinear()
    {
        // A pathological "almost-URL" must be classified as a local path (an upload attempt), not a
        // remote input, and classification must be effectively instant (the ^https?:// regex is
        // anchored and linear — no catastrophic backtracking).
        string pathological = "http" + new string('p', 100_000) + "x"; // not a URL

        var http = new StubSender().Json(201,
            "{\"id\":\"job-1\",\"token\":\"tok\",\"server\":\"https://up/v2\",\"status\":{\"code\":\"incomplete\"}}");
        using var client = new Api2ConvertClient("k", Config.Default, http);

        var stopwatch = Stopwatch.StartNew();
        var ex = await Assert.ThrowsAsync<Api2ConvertException>(() => client.StartConversionAsync(pathological, "png"));
        stopwatch.Stop();

        Assert.Contains("Input file not found", ex.Message, StringComparison.Ordinal);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"classification took {stopwatch.ElapsedMilliseconds}ms (expected linear/instant)");

        // A real URL is still classified as a remote input (started immediately, process=true).
        var http2 = new StubSender().Json(201, "{\"id\":\"job-2\",\"status\":{\"code\":\"downloading\"}}");
        using var client2 = new Api2ConvertClient("k", Config.Default, http2);
        await client2.StartConversionAsync("https://example.com/x", "png");

        using var document = System.Text.Json.JsonDocument.Parse(http2.Requests[0].Body!);
        Assert.True(document.RootElement.GetProperty("process").GetBoolean());
    }
}
