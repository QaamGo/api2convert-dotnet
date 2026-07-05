using System.Text;
using System.Threading.Tasks;
using Api2Convert.Exceptions;
using Api2Convert.Models;
using Xunit;

namespace Api2Convert.Tests;

public sealed class FileUploaderTests : A2CTestBase
{
    private static Job StagedJob() => Job.FromDict(Parse(
        "{\"id\":\"job-9\",\"token\":\"tok-abc\",\"server\":\"https://up.example.com/v2\",\"status\":{\"code\":\"incomplete\"}}"));

    [Fact]
    public async Task UploadIsAuthenticatedWithTheJobTokenNotTheAccountKey()
    {
        Http.AddJson(200, "{\"id\":\"in-1\",\"type\":\"upload\"}");

        await Client().Jobs.UploadAsync(StagedJob(), Encoding.UTF8.GetBytes("hello"));

        Assert.Equal("tok-abc", RequestAt(0).Header("X-Oc-Token"));
        Assert.Equal("", RequestAt(0).Header("X-Oc-Api-Key"));
        Assert.False(RequestAt(0).FollowRedirects);
        Assert.EndsWith("/upload-file/job-9", RequestAt(0).Uri);
    }

    [Fact]
    public async Task UploadFailsWhenTheJobHasNoServerOrToken()
    {
        var job = Job.FromDict(Parse("{\"id\":\"job-1\",\"status\":{\"code\":\"incomplete\"}}"));

        await Assert.ThrowsAsync<Api2ConvertException>(() =>
            Client().Jobs.UploadAsync(job, Encoding.UTF8.GetBytes("hello")));
    }

    [Fact]
    public async Task FilenameIsSanitizedAgainstHeaderInjection()
    {
        Http.AddJson(200, "{\"id\":\"in-1\",\"type\":\"upload\"}");

        // A hostile filename with CR/LF/quote must not break out of the Content-Disposition header.
        await Client().Jobs.UploadAsync(StagedJob(), Encoding.UTF8.GetBytes("x"), "a\"b\r\nX-Injected: 1c");

        string body = RequestAt(0).BodyString();
        Assert.Contains("filename=\"abX-Injected: 1c\"", body);
        Assert.DoesNotContain("\r\nX-Injected", body);
    }
}
