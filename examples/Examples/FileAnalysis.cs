using System;
using System.Text;
using System.Threading.Tasks;
using Api2Convert;

namespace Api2Convert.Examples;

// Guide: File Analysis — read a JPG's metadata as JSON.
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- file-analysis
internal static class FileAnalysis
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        ConversionResult result = await client.ConvertAsync(
            Shared.Jpg,
            "json",
            options: null,
            opts: new ConvertOptions { Category = "metadata" });

        byte[] bytes = await result.ContentsAsync();
        Console.WriteLine($"Metadata JSON ({bytes.Length} bytes):");
        Console.WriteLine(Encoding.UTF8.GetString(bytes));
    }
}
