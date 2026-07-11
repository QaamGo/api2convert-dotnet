using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Api2Convert.Exceptions;
using Api2Convert.Http;
using Api2Convert.Models;

namespace Api2Convert;

/// <summary>
/// A downloadable output file. Returned by <c>client.Download(output)</c> and used internally by
/// <see cref="ConversionResult"/>. No network I/O happens until <see cref="SaveAsync"/> or
/// <see cref="ContentsAsync"/> is called.
/// </summary>
public sealed class FileDownload
{
    private readonly Transport _transport;
    private readonly OutputFile _output;
    private readonly string? _downloadPassword;

    /// <param name="downloadPassword">
    /// password remembered from <c>ConvertAsync()</c> / <c>client.Download()</c>, sent automatically on
    /// download; overridable per call.
    /// </param>
    public FileDownload(Transport transport, OutputFile output, string? downloadPassword)
    {
        _transport = transport;
        _output = output;
        _downloadPassword = downloadPassword;
    }

    /// <summary>The self-contained download URL (no auth required).</summary>
    public string Url => _output.Uri;

    /// <summary>
    /// Stream the file to disk.
    /// </summary>
    /// <param name="pathOrDir">a file path, or a directory (the API filename is used).</param>
    /// <param name="downloadPassword">overrides the password remembered from conversion time.</param>
    /// <returns>the path the file was written to.</returns>
    public async Task<string> SaveAsync(
        string pathOrDir,
        string? downloadPassword = null,
        CancellationToken cancellationToken = default)
    {
        string target = ResolveTarget(pathOrDir);
        string? dir = Path.GetDirectoryName(target);
        try
        {
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new Api2ConvertException($"Could not create directory: {dir}: {e.Message}", e);
        }

        using HttpResponse response = await _transport
            .DownloadAsync(_output.Uri, Headers(downloadPassword), cancellationToken).ConfigureAwait(false);

        // Stream to a sibling temp file and rename over the target only after a clean write+flush. This
        // never truncates the target up front and never destroys a pre-existing complete file on a
        // mid-stream failure — a download either fully replaces the target or leaves it untouched.
        string tempPath = target + ".a2c-" + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            using (FileStream output = File.Create(tempPath))
            {
                await CopyBodyAsync(response.Body, output, cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, target, overwrite: true);
        }
        catch (Exception e)
        {
            // Clean up only the temp file — never the caller's pre-existing target.
            TryDelete(tempPath);
            if (e is IOException or UnauthorizedAccessException)
            {
                throw new Api2ConvertException($"Could not write file: {target}: {e.Message}", e);
            }

            throw;
        }

        return target;
    }

    /// <summary>
    /// Copy the download body to <paramref name="destination"/>, attributing a mid-stream failure to
    /// the correct side: a read fault on the network response surfaces as a typed
    /// <see cref="NetworkException"/> (not a filesystem error), while a write fault propagates as-is
    /// (IOException) for the caller to label. Cancellation propagates unchanged.
    /// </summary>
    private static async Task CopyBodyAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        while (true)
        {
            int read;
            try
            {
                read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is IOException or HttpRequestException)
            {
                throw new NetworkException($"The download was interrupted: {e.Message}", e);
            }

            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Best-effort removal of a partial file left by a failed write. The write already failed, so a
    /// leftover we cannot delete must not mask the original error.
    /// </summary>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // ignore: nothing more we can do, and the real failure is already being surfaced
        }
    }

    /// <summary>
    /// Download the file and return its contents (loads into memory).
    /// </summary>
    /// <param name="downloadPassword">overrides the password remembered from conversion time.</param>
    public async Task<byte[]> ContentsAsync(string? downloadPassword = null, CancellationToken cancellationToken = default)
    {
        using HttpResponse response = await _transport
            .DownloadAsync(_output.Uri, Headers(downloadPassword), cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await CopyBodyAsync(response.Body, buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private string ResolveTarget(string pathOrDir)
    {
        bool looksLikeDir = Directory.Exists(pathOrDir)
            || pathOrDir.EndsWith('/')
            || pathOrDir.EndsWith(Path.DirectorySeparatorChar);

        if (looksLikeDir)
        {
            string name = SafeName(_output.Filename) ?? SafeName(_output.Id) ?? "output";
            return Path.Combine(pathOrDir, name);
        }

        return pathOrDir;
    }

    /// <summary>
    /// Reduce an API-supplied name to a bare filename safe to append to a target directory.
    /// <c>output.Filename</c> / <c>output.Id</c> come straight from the API JSON, so a value like
    /// <c>../../etc/cron.d/evil</c> (or one containing separators or a NUL byte) must never escape the
    /// directory the caller chose. Returns null when nothing usable remains, so the caller falls back.
    /// </summary>
    private static string? SafeName(string? name)
    {
        if (name is null)
        {
            return null;
        }

        // Normalize Windows separators and drop NUL, then take the last path segment so every directory
        // component and any leading "../" is stripped on all platforms; trim surrounding space.
        string normalized = name.Replace("\0", string.Empty, StringComparison.Ordinal).Replace('\\', '/');
        int slash = normalized.LastIndexOf('/');
        string basename = (slash >= 0 ? normalized[(slash + 1)..] : normalized).Trim();
        if (basename.Length == 0 || basename == "." || basename == "..")
        {
            return null;
        }

        return basename;
    }

    private IReadOnlyDictionary<string, string> Headers(string? downloadPassword)
    {
        string? password = downloadPassword ?? _downloadPassword;
        return password is not null
            ? new Dictionary<string, string> { ["X-Oc-Download-Password"] = password }
            : new Dictionary<string, string>(0);
    }
}
