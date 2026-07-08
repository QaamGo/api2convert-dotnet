using System;
using System.IO;
using System.Threading.Tasks;
using Api2Convert;

namespace Api2Convert.Examples;

// Guide: Uploading Files — convert a LOCAL file in one call (the SDK uploads it for you).
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- uploading-files
internal static class UploadingFiles
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        // Write a small local file to convert (any path on disk works here).
        string dir = Shared.OutputDir("uploading-files");
        string src = Path.Combine(dir, "input.png");
        await File.WriteAllBytesAsync(src, Shared.TinyPng);

        // Hand ConvertAsync a local path: it stages the job, uploads the file, then waits.
        ConversionResult result = await client.ConvertAsync(src, "png");
        string path = await result.SaveAsync(dir + Path.DirectorySeparatorChar);
        Console.WriteLine($"Saved: {path}");
    }
}
