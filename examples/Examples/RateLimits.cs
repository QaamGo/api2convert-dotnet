using System;
using System.Threading.Tasks;
using Api2Convert;

namespace Api2Convert.Examples;

// Guide: Rate Limits — read the account's contract information (limits and quotas live here).
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- rate-limits
internal static class RateLimits
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        object? contracts = await client.Contracts.GetAsync();
        Console.WriteLine($"Contracts: {contracts}");
    }
}
