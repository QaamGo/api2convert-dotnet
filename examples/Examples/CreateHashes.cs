using System;
using System.Text;
using System.Threading.Tasks;
using Api2Convert;

namespace Api2Convert.Examples;

// Guide: Create Hashes — compute the SHA-256 checksum of a remote ZIP.
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- create-hashes
internal static class CreateHashes
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        ConversionResult result = await client.ConvertAsync(
            Shared.Zip,
            "sha256",
            options: null,
            opts: new ConvertOptions { Category = "hash" });

        // The result is the hash itself; read it into memory.
        byte[] bytes = await result.ContentsAsync();
        Console.WriteLine($"SHA-256 result ({bytes.Length} bytes): {Encoding.UTF8.GetString(bytes)}");
    }
}
