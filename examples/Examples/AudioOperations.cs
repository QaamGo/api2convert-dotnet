using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Api2Convert;

namespace Api2Convert.Examples;

// Guide: Audio Operations — re-encode a WAV file to stereo AAC at 192 kbps.
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- audio-operations
internal static class AudioOperations
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        ConversionResult result = await client.ConvertAsync(
            Shared.Wav,
            "aac",
            new Dictionary<string, object?>
            {
                ["audio_codec"] = "aac",
                ["audio_bitrate"] = 192,
                ["channels"] = "stereo",
                ["frequency"] = 44100,
            },
            new ConvertOptions { Category = "audio" });

        string path = await result.SaveAsync(Shared.OutputDir("audio-operations") + System.IO.Path.DirectorySeparatorChar);
        Console.WriteLine($"Saved: {path}");
    }
}
