using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Api2Convert.Exceptions;
using Api2Convert.Models;
using Xunit;

namespace Api2Convert.Tests;

public sealed class FileDownloadTests : A2CTestBase
{
    [Fact]
    public async Task AMidStreamNetworkReadFailureIsTypedAndLeavesNoFile()
    {
        // The body yields a few bytes, then the read throws — a mid-stream network failure. It must be
        // a typed NetworkException (not a filesystem "could not write" error) and leave no file behind.
        Http.AddRawStream(200, new FailingReadStream(Encoding.UTF8.GetBytes("PARTIAL")));
        string path = Path.Combine(Path.GetTempPath(), "a2c-" + Path.GetRandomFileName() + ".bin");

        await Assert.ThrowsAsync<NetworkException>(() =>
            Client().Download(OutputFile.Of("o", "https://dl/x", "f.bin")).SaveAsync(path));

        Assert.False(File.Exists(path), "a failed download must not leave a partial file behind");
    }

    [Fact]
    public async Task AFailedDownloadPreservesAPreExistingFile()
    {
        // Streaming to a temp file + atomic rename means a mid-stream failure must NOT destroy a
        // previously-complete file at the target path (File.Create used to truncate it up front).
        Http.AddRawStream(200, new FailingReadStream(Encoding.UTF8.GetBytes("PARTIAL")));
        string path = Path.Combine(Path.GetTempPath(), "a2c-" + Path.GetRandomFileName() + ".bin");
        await File.WriteAllTextAsync(path, "PREEXISTING COMPLETE FILE");
        try
        {
            await Assert.ThrowsAsync<NetworkException>(() =>
                Client().Download(OutputFile.Of("o", "https://dl/x", "f.bin")).SaveAsync(path));

            Assert.Equal("PREEXISTING COMPLETE FILE", await File.ReadAllTextAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ASecretBearingDownloadThatRedirectsThrowsInsteadOfSavingTheRedirectBody()
    {
        // A password makes this the no-follow path; a 3xx passes the < 400 success check, so without a
        // guard the redirect page would be saved as the file. It must surface as a NetworkException.
        Http.AddJson(302, "redirect body", new Dictionary<string, string> { ["Location"] = "https://evil/steal" });

        await Assert.ThrowsAsync<NetworkException>(() =>
            Client().Download(OutputFile.Of("o", "https://dl/x", "f.pdf"), "s3cret").ContentsAsync());
    }

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

    /// <summary>
    /// A read-only stream that returns a fixed prefix once, then throws on the next read — simulating a
    /// connection dropping partway through a download.
    /// </summary>
    private sealed class FailingReadStream : Stream
    {
        private readonly byte[] _prefix;
        private int _position;

        public FailingReadStream(byte[] prefix) => _prefix = prefix;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position < _prefix.Length)
            {
                int n = Math.Min(count, _prefix.Length - _position);
                Array.Copy(_prefix, _position, buffer, offset, n);
                _position += n;
                return n;
            }

            throw new IOException("simulated mid-stream network failure");
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_position < _prefix.Length)
            {
                int n = Math.Min(buffer.Length, _prefix.Length - _position);
                _prefix.AsSpan(_position, n).CopyTo(buffer.Span);
                _position += n;
                return ValueTask.FromResult(n);
            }

            throw new IOException("simulated mid-stream network failure");
        }

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
