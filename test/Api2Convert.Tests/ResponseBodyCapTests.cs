using System;
using System.IO;
using System.Threading.Tasks;
using Api2Convert.Exceptions;
using Api2Convert.Http;
using Xunit;

namespace Api2Convert.Tests;

/// <summary>
/// The control-plane (API / error) JSON body is buffered into memory, so a hostile or buggy server
/// must not be able to force an unbounded read (OOM). These tests prove the SDK caps that read at
/// 16 MiB, rejects an over-cap body with a typed error, and stops reading instead of draining the
/// whole (possibly enormous) stream.
/// </summary>
public sealed class ResponseBodyCapTests : A2CTestBase
{
    private const int MaxResponseBytes = 16 * 1024 * 1024; // 16 MiB — mirrors Transport's cap

    private Api2ConvertClient NoRetry() => Client(new Config.Builder().MaxRetries(0).Build());

    [Fact]
    public async Task OverCapApiBodyIsRejectedAndNotBufferedUnbounded()
    {
        // A 200 whose body would be far larger than the cap: reading it whole would risk an OOM. The
        // stream is lazy so nothing is materialized up front — if the SDK read it unbounded it would
        // pull all of CountingStream's bytes; the cap must stop it well before that.
        var body = new CountingStream(64 * 1024 * 1024);
        Http.AddRawStream(200, body);

        var ex = await Assert.ThrowsAsync<NetworkException>(() => NoRetry().Jobs.GetAsync("job-1"));

        Assert.Contains("exceeds 16 MiB", ex.Message);

        // The SDK must stop at the first over-cap read rather than draining the whole body: at most the
        // cap plus a single read chunk is ever pulled from the network.
        Assert.True(
            body.Produced <= MaxResponseBytes + 81920,
            $"read {body.Produced} bytes; expected at most the 16 MiB cap plus one chunk");
    }

    [Fact]
    public async Task OverCapErrorBodyIsRejectedWithoutOom()
    {
        // The error path (EnsureSuccessfulAsync) reads the body too; a huge error body must be capped
        // the same way rather than buffered whole.
        var body = new CountingStream(64 * 1024 * 1024);
        Http.AddRawStream(500, body);

        var ex = await Assert.ThrowsAsync<NetworkException>(() => NoRetry().Jobs.GetAsync("job-1"));

        Assert.Contains("exceeds 16 MiB", ex.Message);
        Assert.True(body.Produced <= MaxResponseBytes + 81920);
    }

    [Fact]
    public async Task BodyAtTheCapIsAccepted()
    {
        // A body exactly at the cap must still be read; the limit rejects only what exceeds it. Pad a
        // valid job to exactly MaxResponseBytes and confirm it decodes rather than tripping the cap.
        const string prefix = "{\"id\":\"job-1\",\"_pad\":\"";
        const string suffix = "\"}";
        var json = prefix + new string('x', MaxResponseBytes - prefix.Length - suffix.Length) + suffix;
        Http.AddRaw(200, System.Text.Encoding.ASCII.GetBytes(json));

        Models.Job job = await NoRetry().Jobs.GetAsync("job-1");

        Assert.Equal("job-1", job.Id);
    }

    /// <summary>
    /// A lazy, read-only stream that hands out up to <c>total</c> zero bytes on demand and counts how
    /// many it produced. Nothing is allocated up front, so an unbounded reader would be exposed by the
    /// byte count it pulled.
    /// </summary>
    private sealed class CountingStream : Stream
    {
        private readonly long _total;

        public CountingStream(long total) => _total = total;

        public long Produced { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _total;

        public override long Position { get => Produced; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Produced >= _total)
            {
                return 0;
            }

            int n = (int)Math.Min(count, _total - Produced);
            Array.Clear(buffer, offset, n);
            Produced += n;
            return n;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
