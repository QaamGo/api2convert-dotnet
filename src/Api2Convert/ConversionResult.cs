using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Api2Convert.Exceptions;
using Api2Convert.Http;
using Api2Convert.Models;

namespace Api2Convert;

/// <summary>
/// The result of a completed conversion.
///
/// <para>The common case is one output: <c>await result.SaveAsync("out.pdf")</c>. Jobs that produce
/// several files expose them via <see cref="Outputs"/> and <see cref="Download"/>.</para>
/// </summary>
public sealed class ConversionResult
{
    private readonly Transport _transport;
    private readonly int _index;
    private readonly string? _downloadPassword;

    /// <param name="downloadPassword">
    /// password set when the conversion was created; sent automatically when downloading a protected output.
    /// </param>
    public ConversionResult(Job job, Transport transport, int index, string? downloadPassword)
    {
        Job = job;
        _transport = transport;
        _index = index;
        _downloadPassword = downloadPassword;
    }

    /// <summary>The completed job.</summary>
    public Job Job { get; }

    /// <summary>All output files produced by the job.</summary>
    public IReadOnlyList<OutputFile> Outputs => Job.Output;

    /// <summary>The selected output file (the first one by default).</summary>
    public OutputFile Output()
    {
        IReadOnlyList<OutputFile> outputs = Job.Output;
        if (_index < 0 || _index >= outputs.Count)
        {
            throw new Api2ConvertException("The job produced no output files.");
        }

        return outputs[_index];
    }

    /// <summary>The download URL of the selected output (self-contained, no auth).</summary>
    public string Url => Output().Uri;

    /// <summary>
    /// Download the selected output to disk. A download password set at conversion time is applied
    /// automatically; pass one here only to override it.
    /// </summary>
    /// <returns>the path the file was written to.</returns>
    public Task<string> SaveAsync(string pathOrDir, string? downloadPassword = null, CancellationToken cancellationToken = default) =>
        Download().SaveAsync(pathOrDir, downloadPassword, cancellationToken);

    /// <summary>
    /// Download the selected output and return its contents (loads into memory). A download password
    /// set at conversion time is applied automatically; pass one here only to override it.
    /// </summary>
    public Task<byte[]> ContentsAsync(string? downloadPassword = null, CancellationToken cancellationToken = default) =>
        Download().ContentsAsync(downloadPassword, cancellationToken);

    /// <summary>
    /// A <see cref="FileDownload"/> for a specific output (defaults to the selected one). It carries the
    /// download password set at conversion time, so downloads need no password re-supplied.
    /// </summary>
    public FileDownload Download(OutputFile? output = null) =>
        new(_transport, output ?? Output(), _downloadPassword);
}
