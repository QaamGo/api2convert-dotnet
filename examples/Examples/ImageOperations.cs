using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Api2Convert;

namespace Api2Convert.Examples;

// Guide: Image Operations — resize a JPG to fit 800x600, cropping to keep the aspect ratio.
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- image-operations
internal static class ImageOperations
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        ConversionResult result = await client.ConvertAsync(
            Shared.Jpg,
            "resize-image",
            new Dictionary<string, object?>
            {
                ["width"] = 800,
                ["height"] = 600,
                ["resize_by"] = "px",
                ["resize_handling"] = "keep_aspect_ratio_crop",
            },
            new ConvertOptions { Category = "operation" });

        string path = await result.SaveAsync(Shared.OutputDir("image-operations") + System.IO.Path.DirectorySeparatorChar);
        Console.WriteLine($"Saved: {path}");
    }
}
