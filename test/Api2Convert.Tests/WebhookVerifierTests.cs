using System;
using System.Security.Cryptography;
using System.Text;
using Api2Convert.Exceptions;
using Xunit;

namespace Api2Convert.Tests;

public sealed class WebhookVerifierTests
{
    private const string Secret = "whsec_test";
    private const string Payload = "{\"id\":\"job-1\",\"status\":{\"code\":\"completed\"}}";

    private static string Sign(string payload, string secret)
    {
        byte[] hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public void AcceptsAValidSignature()
    {
        var verifier = Api2ConvertClient.Webhooks();

        var evt = verifier.ConstructEvent(Payload, Sign(Payload, Secret), Secret);

        Assert.Equal("job-1", evt.Job.Id);
        Assert.True(evt.Job.IsCompleted);
    }

    [Fact]
    public void RejectsATamperedPayload()
    {
        var verifier = Api2ConvertClient.Webhooks();
        string signature = Sign(Payload, Secret);

        Assert.Throws<SignatureVerificationException>(() =>
            verifier.ConstructEvent(Payload + " ", signature, Secret));
    }

    [Fact]
    public void RejectsAnEqualLengthWrongSignatureWithoutCrashing()
    {
        var verifier = Api2ConvertClient.Webhooks();
        string wrong = new('f', Sign(Payload, Secret).Length);

        Assert.Throws<SignatureVerificationException>(() =>
            verifier.ConstructEvent(Payload, wrong, Secret));
    }

    [Fact]
    public void MissingSignatureWithASecretIsRejected()
    {
        var verifier = Api2ConvertClient.Webhooks();

        Assert.Throws<SignatureVerificationException>(() =>
            verifier.ConstructEvent(Payload, signature: null, Secret));
    }

    [Fact]
    public void EmptySecretIsADeliberateVerificationBypass()
    {
        var verifier = Api2ConvertClient.Webhooks();

        var evt = verifier.ConstructEvent(Payload, signature: null, secret: "");

        Assert.Equal("job-1", evt.Job.Id);
    }

    [Fact]
    public void ParseRejectsANonJsonObjectBody()
    {
        var verifier = Api2ConvertClient.Webhooks();

        Assert.Throws<SignatureVerificationException>(() => verifier.Parse("not json"));
    }
}
