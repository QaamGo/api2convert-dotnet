using System;
using System.IO;
using System.Threading.Tasks;
using Api2Convert;
using Api2Convert.Exceptions;
using Api2Convert.Http;
using Xunit;

namespace Api2Convert.LiveTests;

/// <summary>
/// End-to-end conformance against the real API. Auto-skipped unless <c>API2CONVERT_API_KEY</c> is set
/// (see <see cref="LiveFactAttribute"/>), so it is safe in the default suite.
///
/// <para>Run against a host with:
/// <c>API2CONVERT_API_KEY=&lt;behat key&gt; API2CONVERT_BASE_URL=https://api.web8.api2convert.com/v2
/// dotnet test test/Api2Convert.LiveTests</c>.</para>
/// </summary>
public sealed class ConversionConformanceTests
{
    private const string ExampleJpg =
        "https://example-files.online-convert.com/raster%20image/jpg/example.jpg";

    private static Api2ConvertClient Client()
    {
        string? baseUrl = Environment.GetEnvironmentVariable("API2CONVERT_BASE_URL");
        Config config = string.IsNullOrEmpty(baseUrl)
            ? Config.Default
            : new Config.Builder().BaseUrl(baseUrl).Build();
        return new Api2ConvertClient(Environment.GetEnvironmentVariable("API2CONVERT_API_KEY"), config);
    }

    [LiveFact]
    public async Task ConvertsRemoteImageToPng()
    {
        using Api2ConvertClient client = Client();

        var result = await client.ConvertAsync(ExampleJpg, "png");

        Assert.True(result.Job.IsCompleted);
        string target = Path.Combine(Path.GetTempPath(), "a2c-live-" + Path.GetRandomFileName() + ".png");
        try
        {
            await result.SaveAsync(target);
            Assert.True(new FileInfo(target).Length > 0);
        }
        finally
        {
            File.Delete(target);
        }
    }

    [LiveFact]
    public async Task InvalidTargetRaisesValidationError()
    {
        // The real API rejects an unknown target synchronously at job creation (HTTP 400 ->
        // ValidationException), not as an async failed job. The failed-job -> ConversionFailedException
        // path is covered by the offline unit suite (WaitAndPollingTests).
        using Api2ConvertClient client = Client();

        await Assert.ThrowsAsync<ValidationException>(() => client.ConvertAsync(ExampleJpg, "this-is-not-a-real-target"));
    }
}
