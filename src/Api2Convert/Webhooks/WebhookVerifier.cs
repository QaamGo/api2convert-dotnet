using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api2Convert.Exceptions;
using Api2Convert.Support;

namespace Api2Convert.Webhooks;

/// <summary>
/// Verifies and parses webhook callbacks.
///
/// <para>Pass the <strong>raw</strong> request body (bytes, or the exact string received) so signature
/// verification is byte-exact. Verification uses HMAC-SHA256 and matches the server's signed-webhooks
/// scheme; until signed webhooks are enabled on your account no signature is sent — use
/// <see cref="Parse(byte[])"/> then, or call <see cref="ConstructEvent(byte[], string?, string)"/>
/// with an empty secret to skip verification.</para>
/// </summary>
public sealed class WebhookVerifier
{
    /// <summary>
    /// Verify the signature (when a secret is given) and return the typed event.
    /// </summary>
    /// <param name="payload">the raw request body.</param>
    /// <param name="signature">the signature header value (e.g. <c>X-Oc-Signature</c>).</param>
    /// <param name="secret">your webhook signing secret; pass <c>""</c> to skip verification.</param>
    /// <exception cref="SignatureVerificationException">when the signature is missing or does not match.</exception>
    public WebhookEvent ConstructEvent(byte[] payload, string? signature, string secret)
    {
        if (!string.IsNullOrEmpty(secret))
        {
            if (string.IsNullOrEmpty(signature))
            {
                throw new SignatureVerificationException("Missing webhook signature header.");
            }

            string expected = HmacSha256Hex(payload, secret);

            // Constant-time comparison so a wrong signature cannot be recovered by timing.
            // FixedTimeEquals also short-circuits false on a length mismatch without leaking content.
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected),
                    Encoding.UTF8.GetBytes(signature)))
            {
                throw new SignatureVerificationException("Webhook signature verification failed.");
            }
        }

        return Parse(payload);
    }

    public WebhookEvent ConstructEvent(string payload, string? signature, string secret) =>
        ConstructEvent(Encoding.UTF8.GetBytes(payload), signature, secret);

    /// <summary>
    /// Parse a callback body into a typed event WITHOUT verifying a signature. Only use this when
    /// signed webhooks are not yet enabled for your account.
    /// </summary>
    /// <exception cref="SignatureVerificationException">when the body is not a valid JSON object.</exception>
    public WebhookEvent Parse(byte[] payload)
    {
        object? decoded;
        try
        {
            decoded = Json.Decode(payload);
        }
        catch (JsonException e)
        {
            throw new SignatureVerificationException("Webhook payload is not valid JSON: " + e.Message);
        }

        if (decoded is not IReadOnlyDictionary<string, object?> map)
        {
            throw new SignatureVerificationException("Webhook payload is not a JSON object.");
        }

        return WebhookEvent.FromDict(map);
    }

    public WebhookEvent Parse(string payload) => Parse(Encoding.UTF8.GetBytes(payload));

    private static string HmacSha256Hex(byte[] payload, string secret)
    {
        byte[] raw = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload);
        return Convert.ToHexString(raw).ToLowerInvariant();
    }
}
