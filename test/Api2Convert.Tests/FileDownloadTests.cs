using System.IO;
using System.Text;
using System.Threading.Tasks;
using Api2Convert.Models;
using Xunit;

namespace Api2Convert.Tests;

public sealed class FileDownloadTests : A2CTestBase
{
    [Fact]
    public async Task SavesToAnExplicitFilePath()
    {
        Http.AddRaw(200, Encoding.UTF8.GetBytes("PNGBYTES"));
        string path = Path.Combine(Path.GetTempPath(), "a2c-" + Path.GetRandomFileName() + ".png");
        try
        {
            string written = await Client().Download(OutputFile.Of("o", "https://dl/x", "ignored.png")).SaveAsync(path);

            Assert.Equal(path, written);
            Assert.Equal("PNGBYTES", await File.ReadAllTextAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SavingToADirectoryKeepsTheApiFilename()
    {
        Http.AddRaw(200, Encoding.UTF8.GetBytes("X"));
        DirectoryInfo dir = Directory.CreateTempSubdirectory("a2c");
        try
        {
            string written = await Client().Download(OutputFile.Of("o", "https://dl/x", "result.pdf")).SaveAsync(dir.FullName);

            Assert.Equal(Path.Combine(dir.FullName, "result.pdf"), written);
            Assert.True(File.Exists(written));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task TraversalFilenameIsReducedToItsBasenameInsideTheTargetDirectory()
    {
        Http.AddRaw(200, Encoding.UTF8.GetBytes("X"));
        DirectoryInfo dir = Directory.CreateTempSubdirectory("a2c");
        try
        {
            var output = OutputFile.Of(null, "https://dl/x", "../../../etc/evil");
            string written = await Client().Download(output).SaveAsync(dir.FullName);

            Assert.Equal(Path.Combine(dir.FullName, "evil"), written);
            // The traversal target must not exist outside the chosen directory.
            Assert.False(File.Exists(Path.Combine(dir.FullName, "..", "..", "..", "etc", "evil")));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task DownloadWithPasswordSendsHeaderAndDoesNotFollowRedirects()
    {
        Http.AddRaw(200, Encoding.UTF8.GetBytes("BYTES"));

        await Client().Download(OutputFile.Of("o", "https://dl.example.com/x", "f.pdf"), "s3cret").ContentsAsync();

        Assert.Equal("s3cret", RequestAt(0).Header("X-Oc-Download-Password"));
        Assert.False(RequestAt(0).FollowRedirects);
    }

    [Fact]
    public async Task PasswordlessDownloadMayFollowRedirects()
    {
        Http.AddRaw(200, Encoding.UTF8.GetBytes("BYTES"));

        await Client().Download(OutputFile.Of("o", "https://dl.example.com/x", "f.pdf")).ContentsAsync();

        Assert.True(RequestAt(0).FollowRedirects);
        Assert.Equal("", RequestAt(0).Header("X-Oc-Download-Password"));
    }
}
