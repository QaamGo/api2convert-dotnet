using System.Threading.Tasks;
using Api2Convert.Exceptions;
using Api2Convert.Http;
using Xunit;

namespace Api2Convert.Tests;

public sealed class ErrorMappingTests : A2CTestBase
{
    private Api2ConvertClient NoRetry() => Client(new Config.Builder().MaxRetries(0).Build());

    [Theory]
    [InlineData(401, typeof(AuthenticationException))]
    [InlineData(403, typeof(AuthenticationException))]
    [InlineData(402, typeof(PaymentRequiredException))]
    [InlineData(404, typeof(NotFoundException))]
    [InlineData(400, typeof(ValidationException))]
    [InlineData(422, typeof(ValidationException))]
    [InlineData(429, typeof(RateLimitException))]
    [InlineData(500, typeof(ServerException))]
    [InlineData(503, typeof(ServerException))]
    [InlineData(418, typeof(ApiException))]
    public async Task StatusCodesMapToTypedExceptions(int status, System.Type expected)
    {
        Http.AddJson(status, "{\"message\":\"nope\"}");

        var ex = await Assert.ThrowsAnyAsync<ApiException>(() => NoRetry().Jobs.GetAsync("job-1"));

        Assert.IsType(expected, ex);
        Assert.Equal(status, ex.StatusCode);
        Assert.Equal("nope", ex.Message);
    }

    [Fact]
    public async Task CapturesRequestIdFromTheResponseHeader()
    {
        Http.AddJson(404, "{\"message\":\"missing\"}", new System.Collections.Generic.Dictionary<string, string> { ["X-Request-Id"] = "req-abc" });

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => NoRetry().Jobs.GetAsync("job-1"));

        Assert.Equal("req-abc", ex.RequestId);
        Assert.Equal("missing", ex.Body["message"]);
    }

    [Fact]
    public async Task FallsBackToAGenericMessageWhenTheBodyHasNone()
    {
        Http.AddJson(500, "{}");

        var ex = await Assert.ThrowsAsync<ServerException>(() => NoRetry().Jobs.GetAsync("job-1"));

        Assert.Contains("HTTP 500", ex.Message);
    }

    [Fact]
    public async Task NonJsonSuccessBodySurfacesAsNetworkException()
    {
        Http.AddRaw(200, System.Text.Encoding.UTF8.GetBytes("<html>not json</html>"));

        await Assert.ThrowsAsync<NetworkException>(() => NoRetry().Jobs.GetAsync("job-1"));
    }
}
