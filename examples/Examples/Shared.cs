using System;
using System.IO;
using Api2Convert;
using Api2Convert.Http;

namespace Api2Convert.Examples;

/// <summary>
/// Small helpers shared by every guide example: build a client from the environment, the public
/// remote fixture URLs used across the docs, and a scratch directory for saved output.
/// </summary>
internal static class Shared
{
    // Public example files hosted by online-convert.com — the same fixtures used by the docs guides.
    public const string Pdf = "https://example-files.online-convert.com/document/pdf/example.pdf";
    public const string Png = "https://example-files.online-convert.com/raster%20image/png/example.png";
    public const string Jpg = "https://example-files.online-convert.com/raster%20image/jpg/example.jpg";
    public const string JpgSmall = "https://example-files.online-convert.com/raster%20image/jpg/example_small.jpg";
    public const string Mp4 = "https://example-files.online-convert.com/video/mp4/example.mp4";
    public const string Wav = "https://example-files.online-convert.com/audio/wav/example.wav";
    public const string Docx = "https://example-files.online-convert.com/document/docx/example.docx";
    public const string Zip = "https://example-files.online-convert.com/archive/zip/example.zip";

    /// <summary>A minimal valid 1x1 PNG, used where a guide needs a real local file to upload.</summary>
    public static readonly byte[] TinyPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
        0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x03, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D, 0xB0, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45,
        0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
    };

    /// <summary>
    /// The idiomatic client construction: the key comes from <c>API2CONVERT_API_KEY</c>, and an
    /// optional <c>API2CONVERT_BASE_URL</c> retargets the host (e.g. a beta environment). The key is
    /// only ever read from the environment — never hard-coded or printed.
    /// </summary>
    public static Api2ConvertClient NewClient()
    {
        string? baseUrl = Environment.GetEnvironmentVariable("API2CONVERT_BASE_URL");
        Config config = string.IsNullOrEmpty(baseUrl)
            ? Config.Default
            : new Config.Builder().BaseUrl(baseUrl).Build();
        return new Api2ConvertClient(Environment.GetEnvironmentVariable("API2CONVERT_API_KEY"), config);
    }

    /// <summary>Create a fresh output directory to save example results into, and return its path.</summary>
    public static string OutputDir(string tag)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"a2c-example-{tag}-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
