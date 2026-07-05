using System;
using System.Security.Cryptography;
using System.Text;
using Api2Convert;
using Api2Convert.Exceptions;
using Api2Convert.Models;
using Api2Convert.Webhooks;

// Example webhook receiver logic. Point a job's `callback` at your endpoint, read the RAW request
// body and the `X-Oc-Signature` header, and verify before trusting the payload.
//
// This program simulates the delivery by signing a sample payload with a secret, then runs the same
// verify step your controller would. Wire `rawBody` / `signatureHeader` to your web framework.

const string secret = "whsec_demo_secret";
byte[] rawBody = Encoding.UTF8.GetBytes(
    "{\"id\":\"job-42\",\"status\":{\"code\":\"completed\"}," +
    "\"output\":[{\"id\":\"o1\",\"uri\":\"https://dl.example.com/out.png\",\"filename\":\"out.png\"}]}");

// The server computes this HMAC-SHA256 signature and sends it in the X-Oc-Signature header.
string signatureHeader = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), rawBody)).ToLowerInvariant();

Handle(rawBody, signatureHeader, secret);

static void Handle(byte[] rawBody, string? signatureHeader, string secret)
{
    // Fail closed: an empty secret makes ConstructEvent skip verification entirely, so refuse to run
    // rather than trust an unverified body. If your account has not enabled signed webhooks yet,
    // switch deliberately to Api2ConvertClient.Webhooks().Parse(rawBody) instead.
    if (string.IsNullOrEmpty(secret))
    {
        throw new InvalidOperationException("Webhook secret is not set; refusing to accept unverified webhooks.");
    }

    WebhookEvent evt;
    try
    {
        evt = Api2ConvertClient.Webhooks().ConstructEvent(rawBody, signatureHeader, secret);
    }
    catch (SignatureVerificationException)
    {
        Console.WriteLine("Rejected: bad signature (respond 400 Bad Request).");
        return;
    }

    Job job = evt.Job;
    if (job.IsCompleted)
    {
        foreach (OutputFile output in job.Output)
        {
            Console.WriteLine($"Job {job.Id} done: {output.Uri}");
        }
    }
    else if (job.IsFailed)
    {
        string message = job.Errors.Count == 0 ? "unknown" : job.Errors[0].Message;
        Console.WriteLine($"Job {job.Id} failed: {message}");
    }
    // respond 200 OK
}
