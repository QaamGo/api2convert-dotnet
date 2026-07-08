using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Api2Convert;

namespace Api2Convert.Examples;

// Guide: Convert Files — browse the conversions catalog, then convert a JPG to PNG.
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- convert-files
internal static class ConvertFiles
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        // The full catalog of supported conversions.
        IReadOnlyList<IReadOnlyDictionary<string, object?>> all = await client.Conversions.ListAsync();
        Console.WriteLine($"Catalog entries: {all.Count}");

        // Narrowed to conversions whose target is PNG.
        IReadOnlyList<IReadOnlyDictionary<string, object?>> toPng =
            await client.Conversions.ListAsync(target: "png");
        Console.WriteLine($"Conversions targeting png: {toPng.Count}");

        // Now run one of them: JPG -> PNG.
        ConversionResult result = await client.ConvertAsync(Shared.Jpg, "png");
        string path = await result.SaveAsync(Shared.OutputDir("convert-files") + System.IO.Path.DirectorySeparatorChar);
        Console.WriteLine($"Saved: {path}");
    }
}
