using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Api2Convert;

namespace Api2Convert.Examples;

// Guide: Compress Files — shrink a JPG with the "compress" operation.
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- compress-files
internal static class CompressFiles
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        ConversionResult result = await client.ConvertAsync(
            Shared.Jpg,
            "compress",
            new Dictionary<string, object?> { ["compression_level"] = "high" },
            new ConvertOptions { Category = "operation" });

        string path = await result.SaveAsync(Shared.OutputDir("compress-files") + System.IO.Path.DirectorySeparatorChar);
        Console.WriteLine($"Saved: {path}");
    }
}
