using System;
using System.Threading.Tasks;
using Api2Convert;
using Api2Convert.Models;

namespace Api2Convert.Examples;

// Guide: Extract Assets — pull the embedded assets out of a DOCX document.
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- extract-assets
internal static class ExtractAssets
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        ConversionResult result = await client.ConvertAsync(
            Shared.Docx,
            "extract-assets",
            options: null,
            opts: new ConvertOptions { Category = "operation" });

        Console.WriteLine($"Status: {result.Job.Status.Code}, outputs: {result.Outputs.Count}");
        foreach (OutputFile output in result.Outputs)
        {
            Console.WriteLine($"  {output.Uri}");
        }
    }
}
