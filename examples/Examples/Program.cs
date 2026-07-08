using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Api2Convert.Examples;

// One console app, one runnable example per documented guide. Pick a guide by name:
//
//   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- quickstart
//
// Run with no argument (or an unknown name) to list every guide.

var guides = new Dictionary<string, Func<Task>>(StringComparer.Ordinal)
{
    ["quickstart"] = Quickstart.RunAsync,
    ["convert-files"] = ConvertFiles.RunAsync,
    ["uploading-files"] = UploadingFiles.RunAsync,
    ["job-lifecycle"] = JobLifecycle.RunAsync,
    ["add-watermark"] = AddWatermark.RunAsync,
    ["create-thumbnails"] = CreateThumbnails.RunAsync,
    ["compress-files"] = CompressFiles.RunAsync,
    ["create-archives"] = CreateArchives.RunAsync,
    ["create-hashes"] = CreateHashes.RunAsync,
    ["extract-assets"] = ExtractAssets.RunAsync,
    ["file-analysis"] = FileAnalysis.RunAsync,
    ["compare-files"] = CompareFiles.RunAsync,
    ["capture-website"] = CaptureWebsite.RunAsync,
    ["audio-operations"] = AudioOperations.RunAsync,
    ["image-operations"] = ImageOperations.RunAsync,
    ["webhooks"] = Webhooks.RunAsync,
    ["presets"] = Presets.RunAsync,
    ["statistics"] = Statistics.RunAsync,
    ["rate-limits"] = RateLimits.RunAsync,
    ["authentication"] = Authentication.RunAsync,
};

if (args.Length == 0 || !guides.TryGetValue(args[0], out Func<Task>? run))
{
    if (args.Length > 0)
    {
        Console.Error.WriteLine($"Unknown guide: {args[0]}");
    }

    Console.Error.WriteLine("Usage: dotnet run --project examples/Examples -- <guide>");
    Console.Error.WriteLine("Guides:");
    foreach (string name in guides.Keys.OrderBy(k => k, StringComparer.Ordinal))
    {
        Console.Error.WriteLine($"  {name}");
    }

    return 1;
}

await run();
return 0;
