using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Api2Convert;
using Api2Convert.Models;

namespace Api2Convert.Examples;

// Guide: Authentication — confirm the API key works by listing the account's jobs.
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- authentication
internal static class Authentication
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        // A successful, authenticated call: list this key's jobs.
        IReadOnlyList<Job> jobs = await client.Jobs.ListAsync();
        Console.WriteLine($"Authenticated. Jobs on this account (page 1): {jobs.Count}");
    }
}
