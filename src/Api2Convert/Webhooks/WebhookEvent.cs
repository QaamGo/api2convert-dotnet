using System.Collections.Generic;
using Api2Convert.Models;

namespace Api2Convert.Webhooks;

/// <summary>
/// A verified webhook callback. The API posts the job whose status changed.
/// </summary>
/// <param name="Job">the job whose status changed.</param>
/// <param name="Payload">the full decoded callback body.</param>
public sealed record WebhookEvent(Job Job, IReadOnlyDictionary<string, object?> Payload)
{
    public static WebhookEvent FromDict(IReadOnlyDictionary<string, object?> payload) =>
        new(Job.FromDict(payload), payload);
}
