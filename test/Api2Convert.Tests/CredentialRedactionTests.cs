using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Api2Convert.Enums;
using Api2Convert.Exceptions;
using Api2Convert.Models;
using Xunit;

namespace Api2Convert.Tests;

/// <summary>
/// Cloud-connector fixture 3 — the credential redaction / isolation suite.
///
/// <para>The single secret <c>SUPERSECRET123</c> must never appear on any rendering/error path, and the
/// fixed marker <c>[REDACTED]</c> must appear where a credentials object is rendered.</para>
/// </summary>
public sealed class CredentialRedactionTests : A2CTestBase
{
    private const string Secret = "SUPERSECRET123";
    private const string Marker = "[REDACTED]";

    // ---- 3a: object rendering -------------------------------------------------------------------

    [Fact]
    public void CloudInputToStringMasksCredentials()
    {
        string rendered = CloudInput.AmazonS3("b", "f", "AKIA", Secret).ToString();

        Assert.DoesNotContain(Secret, rendered);
        Assert.Contains(Marker, rendered);
        // Non-secret parameters still render.
        Assert.Contains("\"bucket\":\"b\"", rendered);
    }

    [Fact]
    public void OutputTargetToStringMasksCredentials()
    {
        string rendered = OutputTarget.Of(
            CloudProvider.Ftp,
            new Dictionary<string, object?> { ["host"] = "ftp.example.com" },
            new Dictionary<string, object?> { ["username"] = "u", ["password"] = Secret }).ToString();

        Assert.DoesNotContain(Secret, rendered);
        Assert.Contains(Marker, rendered);
    }

    [Fact]
    public void CloudCredentialsToStringIsAlwaysTheMarker()
    {
        // The opaque wrapper renders the marker even accessed directly (defense in depth for any
        // inspection path that reaches the credentials member).
        var credentials = new CloudCredentials(new Dictionary<string, object?> { ["password"] = Secret });

        Assert.Equal(Marker, credentials.ToString());
        Assert.DoesNotContain(Secret, credentials.ToString());
    }

    // ---- 3b + 3c: error text and error-body deep-walk -------------------------------------------

    [Fact]
    public async Task CreatePathErrorNeverLeaksSubmittedCredential()
    {
        // A 422 whose decoded body echoes the submitted secret in a nested/dotted key (belt-and-
        // suspenders: the real API echoes field *names* only). The ConvertAsync request body itself
        // carried the secret in credentials — it must not surface on the exception either.
        Http.AddJson(422, "{\"message\":\"Validation failed\","
            + "\"errors\":{\"input.0.credentials.secretaccesskey\":\"" + Secret + "\"}}");

        ValidationException error = await Assert.ThrowsAsync<ValidationException>(() =>
            Client().ConvertAsync(CloudInput.AmazonS3("b", "f", "AKIA", Secret), "jpg"));

        // 3b: no secret in the message or anywhere on the exception.
        Assert.DoesNotContain(Secret, error.Message);

        // 3c: the deep-walk masks the echoed secret to the marker.
        string body = JsonSerializer.Serialize((object)error.Body);
        Assert.DoesNotContain(Secret, body);
        Assert.Contains(Marker, body);
    }

    // ---- 3d: sensitive parameters leaf ----------------------------------------------------------

    [Fact]
    public void SensitiveParametersLeafIsMaskedInRendering()
    {
        string rendered = CloudInput.Of(
            CloudProvider.AmazonS3,
            new Dictionary<string, object?> { ["token"] = "PARAMSECRET", ["bucket"] = "b" }).ToString();

        Assert.DoesNotContain("PARAMSECRET", rendered);
        Assert.Contains(Marker, rendered);
        // A non-secret key renders normally.
        Assert.Contains("\"bucket\":\"b\"", rendered);
    }
}
