using System.Threading.Tasks;
using Api2Convert.Exceptions;
using Api2Convert.Http;
using Xunit;

namespace Api2Convert.Tests;

public sealed class WaitAndPollingTests : A2CTestBase
{
    [Fact]
    public async Task FailedJobThrowsConversionFailedExceptionCarryingTheJob()
    {
        Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"failed\"},\"errors\":[{\"code\":4,\"message\":\"bad input\"}]}");

        var ex = await Assert.ThrowsAsync<ConversionFailedException>(() => Client().Jobs.WaitAsync("job-1"));

        Assert.True(ex.Job.IsFailed);
        Assert.Single(ex.Errors);
        Assert.Equal("bad input", ex.Errors[0].Message);
        Assert.Equal(4, ex.Errors[0].Code);
    }

    [Fact]
    public async Task ThrowOnFailureFalseReturnsTheFailedJobInstead()
    {
        Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"failed\"}}");

        var job = await Client().Jobs.WaitAsync("job-1", throwOnFailure: false);

        Assert.True(job.IsFailed);
    }

    [Fact]
    public async Task TimeoutThrowsConversionTimeoutExceptionWhenTheJobNeverTerminates()
    {
        Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"processing\"}}");

        // A zero poll timeout makes the deadline elapse after the first non-terminal poll.
        var config = new Config.Builder().PollTimeout(0).Build();
        var ex = await Assert.ThrowsAsync<ConversionTimeoutException>(() => Client(config).Jobs.WaitAsync("job-1"));

        Assert.Equal("job-1", ex.Job.Id);
    }

    [Fact]
    public async Task PollIntervalIsFlooredSoItCannotBusyLoop()
    {
        Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"processing\"}}");
        Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"completed\"}}");

        // Ask for an abusive 1ms interval; the client floors it to MIN_POLL_INTERVAL.
        var config = new Config.Builder().PollInterval(0.001).Build();
        await Client(config).Jobs.WaitAsync("job-1");

        Assert.Single(Sleeper.Slept);
        Assert.Equal(Config.MinPollInterval, Sleeper.Slept[0]); // no jitter (rng == 0)
    }
}
