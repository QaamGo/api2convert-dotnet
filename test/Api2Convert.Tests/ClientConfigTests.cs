using System;
using System.Threading.Tasks;
using Api2Convert.Exceptions;
using Api2Convert.Http;
using Xunit;

namespace Api2Convert.Tests;

public sealed class ClientConfigTests : A2CTestBase
{
    [Fact]
    public void MissingApiKeyThrows()
    {
        string? saved = Environment.GetEnvironmentVariable("API2CONVERT_API_KEY");
        Environment.SetEnvironmentVariable("API2CONVERT_API_KEY", null);
        try
        {
            // A missing key is an SDK configuration error, not a generic framework ArgumentException:
            // it derives from Api2ConvertException so a single catch handles every SDK failure.
            ConfigurationException ex = Assert.Throws<ConfigurationException>(() => new Api2ConvertClient(""));
            Assert.IsAssignableFrom<Api2ConvertException>(ex);
        }
        finally
        {
            Environment.SetEnvironmentVariable("API2CONVERT_API_KEY", saved);
        }
    }

    [Fact]
    public async Task FallsBackToTheApi2ConvertApiKeyEnvVar()
    {
        string? saved = Environment.GetEnvironmentVariable("API2CONVERT_API_KEY");
        Environment.SetEnvironmentVariable("API2CONVERT_API_KEY", "env-key");
        try
        {
            Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"completed\"}}");
            using var client = new Api2ConvertClient(null, Config.Default, Http, Sleeper, () => 0.0);

            await client.Jobs.GetAsync("job-1");

            Assert.Equal("env-key", RequestAt(0).Header("X-Api2convert-Api-Key"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("API2CONVERT_API_KEY", saved);
        }
    }

    [Fact]
    public void BaseUrlTrailingSlashIsTrimmed()
    {
        var config = new Config.Builder().BaseUrl("https://api.example.com/v2/").Build();
        Assert.Equal("https://api.example.com/v2", config.BaseUrl);
    }

    [Fact]
    public void PollTimeoutIsCappedAtTheCeiling()
    {
        var config = new Config.Builder().PollTimeout(999_999).Build();
        Assert.Equal(Config.MaxPollTimeout, config.PollTimeoutSeconds);
    }

    [Fact]
    public void PollIntervalAndTimeoutAreClampedToSaneFloors()
    {
        var config = new Config.Builder().PollInterval(0).Timeout(0).MaxRetries(-5).Build();
        Assert.Equal(Config.MinPollInterval, config.PollInterval);
        Assert.True(config.TimeoutSeconds >= 1);
        Assert.Equal(0, config.MaxRetries);
    }

    [Fact]
    public async Task UsesTheConfiguredBaseUrl()
    {
        Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"completed\"}}");
        var config = new Config.Builder().BaseUrl("https://api.web8.api2convert.com/v2").Build();

        await Client(config).Jobs.GetAsync("job-1");

        Assert.StartsWith("https://api.web8.api2convert.com/v2/jobs/job-1", RequestAt(0).Uri);
    }
}
