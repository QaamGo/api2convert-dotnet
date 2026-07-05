using System.Threading.Tasks;
using Api2Convert.Exceptions;
using Api2Convert.Http;
using Xunit;

namespace Api2Convert.Tests;

public sealed class RetryTests : A2CTestBase
{
    private Config Retries(int n) => new Config.Builder().MaxRetries(n).Build();

    [Fact]
    public async Task RetriesA429OnAnyMethodThenSucceeds()
    {
        Http.AddJson(429, "{\"message\":\"slow down\"}");
        Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"downloading\"}}");

        var job = await Client(Retries(2)).Jobs.CreateAsync(new System.Collections.Generic.Dictionary<string, object?>());

        Assert.Equal("job-1", job.Id);
        Assert.Equal(2, Http.Requests.Count);
        Assert.Single(Sleeper.Slept); // one backoff between the two attempts
    }

    [Fact]
    public async Task RetriesA5xxForAnIdempotentGet()
    {
        Http.AddJson(503, "{\"message\":\"unavailable\"}");
        Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"completed\"}}");

        var job = await Client(Retries(2)).Jobs.GetAsync("job-1");

        Assert.Equal("job-1", job.Id);
        Assert.Equal(2, Http.Requests.Count);
    }

    [Fact]
    public async Task DoesNotRetryABarePostOn5xx()
    {
        Http.AddJson(500, "{\"message\":\"boom\"}");

        await Assert.ThrowsAsync<ServerException>(() =>
            Client(Retries(3)).Jobs.CreateAsync(new System.Collections.Generic.Dictionary<string, object?>()));

        Assert.Single(Http.Requests); // a non-idempotent POST is never blindly retried on 5xx
    }

    [Fact]
    public async Task RetriesAPostWhenItCarriesAnIdempotencyKey()
    {
        Http.AddJson(500, "{\"message\":\"boom\"}");
        Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"completed\"}}");

        var job = await Client(Retries(2)).Jobs.CreateAsync(
            new System.Collections.Generic.Dictionary<string, object?>(),
            idempotencyKey: "key-123");

        Assert.Equal("job-1", job.Id);
        Assert.Equal(2, Http.Requests.Count);
    }

    [Fact]
    public async Task HonorsRetryAfterHeaderInsteadOfExponentialBackoff()
    {
        Http.AddJson(429, "{\"message\":\"slow down\"}", new System.Collections.Generic.Dictionary<string, string> { ["Retry-After"] = "7" });
        Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"completed\"}}");

        await Client(Retries(2)).Jobs.GetAsync("job-1");

        Assert.Single(Sleeper.Slept);
        Assert.Equal(7.0, Sleeper.Slept[0]);
    }

    [Fact]
    public async Task NetworkErrorIsRetriedForIdempotentRequestsThenThrows()
    {
        // An IHttpSender surfaces a transport failure as a NetworkException (as the real
        // HttpClientSender does); the transport retries that for an idempotent GET, then rethrows.
        Http.AddException(new NetworkException("connection reset"));
        Http.AddException(new NetworkException("connection reset"));

        await Assert.ThrowsAsync<NetworkException>(() => Client(Retries(1)).Jobs.GetAsync("job-1"));

        Assert.Equal(2, Http.Requests.Count); // initial attempt + one retry
    }

    [Fact]
    public async Task ThrowsRateLimitExceptionWhenRetriesAreExhausted()
    {
        Http.AddJson(429, "{\"message\":\"slow down\"}", new System.Collections.Generic.Dictionary<string, string> { ["Retry-After"] = "3" });

        var ex = await Assert.ThrowsAsync<RateLimitException>(() =>
            Client(Retries(0)).Jobs.GetAsync("job-1"));

        Assert.Equal(429, ex.StatusCode);
        Assert.Equal(3, ex.RetryAfter);
    }
}
