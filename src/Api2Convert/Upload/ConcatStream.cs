using System;
using System.Collections.Generic;
using System.IO;

namespace Api2Convert.Upload;

/// <summary>
/// A forward-only read stream that concatenates several source streams in order (the .NET analog of
/// Java's <c>SequenceInputStream</c>). Used to stream a multipart body — preamble, file, epilogue —
/// without buffering the whole thing in memory. Disposing it disposes every source.
/// </summary>
internal sealed class ConcatStream : Stream
{
    private readonly IReadOnlyList<Stream> _sources;
    private int _index;
    private bool _disposed;

    public ConcatStream(params Stream[] sources) => _sources = sources;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        while (_index < _sources.Count)
        {
            int read = _sources[_index].Read(buffer, offset, count);
            if (read > 0)
            {
                return read;
            }

            _index++;
        }

        return 0;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;
            foreach (Stream source in _sources)
            {
                source.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
