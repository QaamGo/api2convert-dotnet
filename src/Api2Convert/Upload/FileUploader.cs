using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Api2Convert.Exceptions;
using Api2Convert.Http;
using Api2Convert.Models;
using Api2Convert.Support;

namespace Api2Convert.Upload;

/// <summary>
/// Uploads a local file to a job's per-job upload server.
///
/// <para>This step is intentionally hand-written: it is NOT described by the OpenAPI spec. It posts a
/// <c>multipart/form-data</c> body (field <c>file</c>) to <c>{job.Server}/upload-file/{job.Id}</c> and
/// authenticates with the per-job <c>X-Oc-Token</c> header — <strong>never the account API
/// key</strong>. The body is streamed, so large files do not have to be read into memory.</para>
/// </summary>
public sealed class FileUploader
{
    private readonly Transport _transport;

    public FileUploader(Transport transport) => _transport = transport;

    /// <param name="file">a path <see cref="string"/>, a <see cref="FileInfo"/>, a <c>byte[]</c>, or a <see cref="Stream"/>.</param>
    /// <param name="filename">name advertised to the API (defaults to the path's basename, else <c>"file"</c>).</param>
    public async Task<InputFile> UploadAsync(
        Job job,
        object file,
        string? filename = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(job.Server) || job.Token is null)
        {
            throw new Api2ConvertException(
                "Cannot upload: the job has no upload server/token. "
                + "Create the job with process=false and upload before starting it.");
        }

        (string name, Func<Stream> openStream) = Resolve(file, filename);
        string safeName = SanitizeFilename(name);

        string boundary = "----api2convertBoundary" + Guid.NewGuid().ToString("N");
        byte[] preamble = Encoding.UTF8.GetBytes(
            "--" + boundary + "\r\n"
            + "Content-Disposition: form-data; name=\"file\"; filename=\"" + safeName + "\"\r\n"
            + "Content-Type: application/octet-stream\r\n\r\n");
        byte[] epilogue = Encoding.UTF8.GetBytes("\r\n--" + boundary + "--\r\n");

        Stream StreamFactory() =>
            new ConcatStream(new MemoryStream(preamble), openStream(), new MemoryStream(epilogue));

        string server = job.Server.TrimEnd('/');
        string url = server + "/upload-file/" + job.Id;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Oc-Token"] = job.Token,
            ["Content-Type"] = "multipart/form-data; boundary=" + boundary,
        };

        HttpRequest request = HttpRequest.Streaming("POST", url, headers, StreamFactory);
        using HttpResponse response = await _transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return InputFile.FromDict(Data.Object(await _transport.InterpretAsync(response, cancellationToken).ConfigureAwait(false)));
    }

    private static (string Name, Func<Stream> Open) Resolve(object file, string? filename)
    {
        switch (file)
        {
            case string path:
                return ResolvePath(path, filename);
            case FileInfo info:
                return ResolvePath(info.FullName, filename);
            case byte[] bytes:
                return (filename ?? "file", () => new MemoryStream(bytes));
            case Stream stream:
                // A caller-supplied stream is one-shot: the request is not replayable (never retried).
                return (filename ?? "file", () => stream);
            default:
                throw new Api2ConvertException(
                    "Unsupported upload input: expected a path string, FileInfo, byte[] or Stream.");
        }
    }

    private static (string Name, Func<Stream> Open) ResolvePath(string path, string? filename)
    {
        if (!File.Exists(path))
        {
            throw new Api2ConvertException("Input file not found: " + path);
        }

        string name = filename ?? Path.GetFileName(path);
        return (name, () => File.OpenRead(path));
    }

    /// <summary>
    /// Neutralize a filename before it is placed in the multipart <c>Content-Disposition</c> header:
    /// strip CR/LF/NUL and double-quotes so a hostile name cannot inject extra headers or break out
    /// of the quoted-string. Falls back to <c>"file"</c> when nothing usable remains.
    /// </summary>
    private static string SanitizeFilename(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (c is not ('\r' or '\n' or '\0' or '"'))
            {
                builder.Append(c);
            }
        }

        string cleaned = builder.ToString().Trim();
        return cleaned.Length == 0 ? "file" : cleaned;
    }
}
