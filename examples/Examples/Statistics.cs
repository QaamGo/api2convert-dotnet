using System;
using System.Threading.Tasks;
using Api2Convert;

namespace Api2Convert.Examples;

// Guide: Statistics — fetch conversion usage statistics for a given month.
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- statistics
internal static class Statistics
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        object? stats = await client.Stats.MonthAsync("2026-06");
        Console.WriteLine($"Statistics for 2026-06: {stats}");
    }
}
