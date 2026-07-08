using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Api2Convert;
using Api2Convert.Models;

namespace Api2Convert.Examples;

// Guide: Add a Watermark — stamp a PNG onto a PDF (a compound job with two remote inputs).
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- add-watermark
internal static class AddWatermark
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        Job job = await client.Jobs.CreateAsync(new Dictionary<string, object?>
        {
            ["process"] = true,
            ["input"] = new List<object?>
            {
                new Dictionary<string, object?> { ["type"] = "remote", ["source"] = Shared.Pdf },
                new Dictionary<string, object?> { ["type"] = "remote", ["source"] = Shared.Png },
            },
            ["conversion"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["category"] = "document",
                    ["target"] = "pdf",
                    ["options"] = new Dictionary<string, object?>
                    {
                        ["stamp"] = true,
                        ["alignment"] = "center",
                    },
                },
            },
        });

        Job finished = await client.Jobs.WaitAsync(job.Id);
        Console.WriteLine($"Status: {finished.Status.Code}, outputs: {finished.Output.Count}");
        if (finished.Output.Count > 0)
        {
            string path = await client.Download(finished.Output[0]).SaveAsync(
                Shared.OutputDir("add-watermark") + System.IO.Path.DirectorySeparatorChar);
            Console.WriteLine($"Saved: {path}");
        }
    }
}
