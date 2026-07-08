using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Api2Convert;
using Api2Convert.Exceptions;
using Api2Convert.Models;
using Api2Convert.Webhooks;

namespace Api2Convert.Examples;

// Guide: Webhooks — start an async conversion with a callback URL, and verify the delivered webhook.
// Run:   API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- webhooks
internal static class Webhooks
{
    public static async Task RunAsync()
    {
        using Api2ConvertClient client = Shared.NewClient();

        // Start a job without waiting; the API will POST to the callback URL when it finishes.
        Job job = await client.StartConversionAsync(
            Shared.Docx,
            "pdf",
            opts: new AsyncOptions
            {
                Category = "document",
                Callback = "https://your-app.example.com/api2convert/webhook",
            });
        Console.WriteLine($"Started job {job.Id} (status: {job.Status.Code}); a webhook will fire on completion.");

        // In your webhook endpoint, verify the RAW request body against your signing secret before
        // trusting it. This simulates a delivery so the verify step below is runnable in isolation.
        const string secret = "whsec_demo_secret";
        byte[] rawBody = Encoding.UTF8.GetBytes(
            "{\"id\":\"job-42\",\"status\":{\"code\":\"completed\"}," +
            "\"output\":[{\"id\":\"o1\",\"uri\":\"https://dl.example.com/out.pdf\",\"filename\":\"out.pdf\"}]}");
        string signatureHeader = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), rawBody)).ToLowerInvariant();

        try
        {
            WebhookEvent evt = Api2ConvertClient.Webhooks().ConstructEvent(rawBody, signatureHeader, secret);
            if (evt.Job.IsCompleted)
            {
                foreach (OutputFile output in evt.Job.Output)
                {
                    Console.WriteLine($"Webhook verified — job {evt.Job.Id} done: {output.Uri}");
                }
            }
        }
        catch (SignatureVerificationException)
        {
            Console.WriteLine("Rejected: bad signature (respond 400 Bad Request).");
        }
    }
}
