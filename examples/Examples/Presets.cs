using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Api2Convert;
using Api2Convert.Models;

namespace Api2Convert.Examples;

// Guide: Presets — list your saved conversion presets for a category and target.
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- presets
internal static class Presets
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        IReadOnlyList<Preset> presets = await client.Presets.ListAsync(category: "video", target: "mp4");
        Console.WriteLine($"Presets (video -> mp4): {presets.Count}");
        foreach (Preset preset in presets)
        {
            Console.WriteLine($"  {preset.Name} -> {preset.Target}");
        }
    }
}
