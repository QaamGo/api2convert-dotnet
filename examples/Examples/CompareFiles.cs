using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Api2Convert;
using Api2Convert.Models;

namespace Api2Convert.Examples;

// Guide: Compare Files — diff two images with SSIM and produce a visual difference map.
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- compare-files
internal static class CompareFiles
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        Job job = await client.Jobs.CreateAsync(new Dictionary<string, object?>
        {
            ["process"] = true,
            ["input"] = new List<object?>
            {
                new Dictionary<string, object?> { ["type"] = "remote", ["source"] = Shared.JpgSmall },
                new Dictionary<string, object?> { ["type"] = "remote", ["source"] = Shared.Jpg },
            },
            ["conversion"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["category"] = "operation",
                    ["target"] = "compare-image",
                    ["options"] = new Dictionary<string, object?>
                    {
                        ["method"] = "ssim",
                        ["threshold"] = 5,
                        ["diff_color"] = "red",
                    },
                },
            },
        });

        Job finished = await client.Jobs.WaitAsync(job.Id);
        Console.WriteLine($"Status: {finished.Status.Code}, outputs: {finished.Output.Count}");
    }
}
