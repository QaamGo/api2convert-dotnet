using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Api2Convert;
using Api2Convert.Models;

namespace Api2Convert.Examples;

// Guide: Capture a Website — screenshot a URL and deliver it as a PNG.
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- capture-website
internal static class CaptureWebsite
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        Job job = await client.Jobs.CreateAsync(new Dictionary<string, object?>
        {
            ["process"] = true,
            ["input"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "remote",
                    ["source"] = "https://www.online-convert.com",
                    ["engine"] = "screenshot",
                    ["options"] = new Dictionary<string, object?>
                    {
                        ["screen_width"] = 1280,
                        ["screen_height"] = 1024,
                        ["device_scale_factor"] = 1,
                    },
                },
            },
            ["conversion"] = new List<object?>
            {
                new Dictionary<string, object?> { ["category"] = "image", ["target"] = "png" },
            },
        });

        Job finished = await client.Jobs.WaitAsync(job.Id);
        Console.WriteLine($"Status: {finished.Status.Code}, outputs: {finished.Output.Count}");
        if (finished.Output.Count > 0)
        {
            string path = await client.Download(finished.Output[0]).SaveAsync(
                Shared.OutputDir("capture-website") + System.IO.Path.DirectorySeparatorChar);
            Console.WriteLine($"Saved: {path}");
        }
    }
}
