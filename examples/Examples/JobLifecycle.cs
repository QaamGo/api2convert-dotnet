using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Api2Convert;
using Api2Convert.Models;

namespace Api2Convert.Examples;

// Guide: Job Lifecycle — drive create -> add input -> start -> wait -> outputs by hand.
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- job-lifecycle
internal static class JobLifecycle
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        // Stage a job (process: false) so inputs can be attached before it starts.
        Job job = await client.Jobs.CreateAsync(new Dictionary<string, object?>
        {
            ["process"] = false,
            ["conversion"] = new List<object?>
            {
                new Dictionary<string, object?> { ["category"] = "image", ["target"] = "png" },
            },
        });
        Console.WriteLine($"Created job {job.Id}");

        // Attach a remote input, then start processing.
        await client.Jobs.AddInputAsync(
            job.Id,
            new Dictionary<string, object?> { ["type"] = "remote", ["source"] = Shared.Jpg });
        await client.Jobs.StartAsync(job.Id);

        // Poll to completion and read the outputs.
        Job finished = await client.Jobs.WaitAsync(job.Id);
        IReadOnlyList<OutputFile> outputs = await client.Jobs.OutputsAsync(finished.Id);
        Console.WriteLine($"Status: {finished.Status.Code}, outputs: {outputs.Count}");
        foreach (OutputFile output in outputs)
        {
            Console.WriteLine($"  {output.Uri}");
        }
    }
}
