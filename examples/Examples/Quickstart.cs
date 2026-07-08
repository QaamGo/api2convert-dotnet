using System;
using System.Threading.Tasks;
using Api2Convert;
using Api2Convert.Models;

namespace Api2Convert.Examples;

// Guide: Quickstart — convert a remote JPG to PNG, look the job up, then download the result.
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- quickstart
internal static class Quickstart
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        // One call creates the job, waits for it, and gives back a downloadable result.
        ConversionResult result = await client.ConvertAsync(Shared.Jpg, "png");

        // Look the finished job up by id (the same object you already hold — shown for illustration).
        Job job = await client.Jobs.GetAsync(result.Job.Id);
        Console.WriteLine($"Job {job.Id} status: {job.Status.Code}");

        // Download the output to a local directory.
        string dir = Shared.OutputDir("quickstart");
        string path = await result.SaveAsync(dir + System.IO.Path.DirectorySeparatorChar);
        Console.WriteLine($"Saved: {path}");
    }
}
