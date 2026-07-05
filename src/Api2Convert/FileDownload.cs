using System;
using System.Collections.Generic;
using System.IO;
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

        try
        {
            using HttpResponse response = await _transport
                .DownloadAsync(_output.Uri, Headers(downloadPassword), cancellationToken).ConfigureAwait(false);
            using FileStream output = File.Create(target);
            await response.Body.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new Api2ConvertException($"Could not write file: {target}: {e.Message}", e);
        }

        return target;
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
        await response.Body.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
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
