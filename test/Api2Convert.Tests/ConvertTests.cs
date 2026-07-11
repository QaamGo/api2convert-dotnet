using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Api2Convert.Tests;

public sealed class ConvertTests : A2CTestBase
{
    private const string CompletedJob =
        "{\"id\":\"job-1\",\"status\":{\"code\":\"completed\"},"
        + "\"output\":[{\"id\":\"o1\",\"uri\":\"https://dl.example.com/out.png\",\"filename\":\"out.png\"}]}";

    [Fact]
    public async Task ConvertFromUrlStartsASingleRemoteJobThenPolls()
    {
        Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"downloading\"}}");
        Http.AddJson(200, CompletedJob);

        var result = await Client().ConvertAsync("https://example.com/in.jpg", "png");

        Assert.True(result.Job.IsCompleted);
        Assert.Equal("https://dl.example.com/out.png", result.Url);

        // First request creates a started remote job; no upload happens for a URL.
        Assert.Equal("POST", RequestAt(0).Method);
        Assert.EndsWith("/v2/jobs", RequestAt(0).Uri);
        IReadOnlyDictionary<string, object?> body = RequestAt(0).BodyJson();
        Assert.Equal(true, body["process"]);
        Assert.Equal("test-key", RequestAt(0).Header("X-Api2convert-Api-Key"));

        // Second request polls the job.
        Assert.Equal("GET", RequestAt(1).Method);
        Assert.EndsWith("/v2/jobs/job-1", RequestAt(1).Uri);
    }

    [Fact]
    public async Task ConvertLocalFileStagesUploadsStartsThenPolls()
    {
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "hello");
        try
        {
            Http.AddJson(200, "{\"id\":\"job-1\",\"token\":\"tok\",\"server\":\"https://up.example.com/v2\",\"status\":{\"code\":\"incomplete\"}}");
            Http.AddJson(200, "{\"id\":\"in-1\",\"type\":\"upload\"}");
            Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"processing\"}}");
            Http.AddJson(200, CompletedJob);

            var result = await Client().ConvertAsync(path, "png");

            Assert.True(result.Job.IsCompleted);

            // create (process=false) -> upload -> start (PATCH) -> poll (GET)
            Assert.Equal("POST", RequestAt(0).Method);
            Assert.Equal(false, RequestAt(0).BodyJson()["process"]);
            Assert.EndsWith("/upload-file/job-1", RequestAt(1).Uri);
            Assert.Equal("tok", RequestAt(1).Header("X-Api2convert-Token"));
            Assert.Equal("PATCH", RequestAt(2).Method);
            Assert.Equal("GET", RequestAt(3).Method);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ConversionOptionsAreSentUnderTheConversionOptionsKey()
    {
        Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"downloading\"}}");
        Http.AddJson(200, CompletedJob);

        var options = new Dictionary<string, object?> { ["quality"] = 80L, ["strip"] = true };
        await Client().ConvertAsync("https://example.com/in.jpg", "jpg", options);

        IReadOnlyDictionary<string, object?> body = RequestAt(0).BodyJson();
        var conversions = Assert.IsAssignableFrom<IReadOnlyList<object?>>(body["conversion"]);
        var first = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(conversions[0]);
        Assert.Equal("jpg", first["target"]);
        var sentOptions = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(first["options"]);
        Assert.Equal(80L, System.Convert.ToInt64(sentOptions["quality"]));
        Assert.Equal(true, sentOptions["strip"]);
        // The options ride under conversion.options, not mixed into the SDK control keys.
        Assert.Contains("\"quality\":80", RequestAt(0).BodyString());
    }

    [Fact]
    public async Task StartConversionReturnsAfterStartWithoutPolling()
    {
        Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"downloading\"}}");

        var job = await Client().StartConversionAsync("https://example.com/in.jpg", "png");

        Assert.Equal("job-1", job.Id);
        Assert.False(job.IsTerminal);
        Assert.Single(Http.Requests); // no poll
    }

    [Fact]
    public async Task StartConversionWithCallbackSetsNotifyStatus()
    {
        Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"downloading\"}}");

        await Client().StartConversionAsync(
            "https://example.com/in.jpg",
            "png",
            opts: new AsyncOptions { Callback = "https://app.example.com/hook" });

        IReadOnlyDictionary<string, object?> body = RequestAt(0).BodyJson();
        Assert.Equal("https://app.example.com/hook", body["callback"]);
        Assert.Equal(true, body["notify_status"]);
    }

    [Fact]
    public async Task DownloadPasswordIsSentAsDownloadPasswordsAndRememberedOnTheResult()
    {
        Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"downloading\"}}");
        Http.AddJson(200, CompletedJob);
        Http.AddRaw(200, System.Text.Encoding.UTF8.GetBytes("PDFBYTES"));

        var result = await Client().ConvertAsync(
            "https://example.com/in.jpg",
            "png",
            opts: new ConvertOptions { DownloadPassword = "s3cret" });

        // download_passwords set on create
        var passwords = Assert.IsAssignableFrom<IReadOnlyList<object?>>(RequestAt(0).BodyJson()["download_passwords"]);
        Assert.Equal("s3cret", passwords[0]);

        // password is remembered: the download auto-sends it
        await result.ContentsAsync();
        Assert.Equal("s3cret", Http.Last().Header("X-Api2convert-Download-Password"));
    }
}
