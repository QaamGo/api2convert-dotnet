using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Api2Convert;
using Api2Convert.Models;

namespace Api2Convert.Examples;

// Guide: Create Archives — bundle two remote files into a single ZIP.
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- create-archives
internal static class CreateArchives
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
                new Dictionary<string, object?> { ["category"] = "archive", ["target"] = "zip" },
            },
        });

        Job finished = await client.Jobs.WaitAsync(job.Id);
        Console.WriteLine($"Status: {finished.Status.Code}, outputs: {finished.Output.Count}");
        if (finished.Output.Count > 0)
        {
            string path = await client.Download(finished.Output[0]).SaveAsync(
                Shared.OutputDir("create-archives") + System.IO.Path.DirectorySeparatorChar);
            Console.WriteLine($"Saved: {path}");
        }
    }
}
