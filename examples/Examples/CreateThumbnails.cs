using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Api2Convert;

namespace Api2Convert.Examples;

// Guide: Create Thumbnails — render the first page of a PDF to a 300px PNG thumbnail.
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- create-thumbnails
internal static class CreateThumbnails
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        ConversionResult result = await client.ConvertAsync(
            Shared.Pdf,
            "thumbnail",
            new Dictionary<string, object?>
            {
                ["thumbnail_target"] = "png",
                ["width"] = 300,
                ["pages"] = "first",
                ["dpi"] = 150,
            },
            new ConvertOptions { Category = "operation" });

        string path = await result.SaveAsync(Shared.OutputDir("create-thumbnails") + System.IO.Path.DirectorySeparatorChar);
        Console.WriteLine($"Saved: {path}");
    }
}
