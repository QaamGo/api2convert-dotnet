using Api2Convert.Models;

namespace Api2Convert.Exceptions;

/// <summary>
/// A job did not reach a terminal status within the configured poll timeout.
///
/// <para>The job is still running server-side — re-fetch it later with
/// <c>client.Jobs.GetAsync(job.Id)</c> or raise the timeout.</para>
///
/// <para>Named <c>ConversionTimeoutException</c> (not <c>TimeoutException</c>) to avoid colliding with
/// <see cref="System.TimeoutException"/> — a documented C#-idiom divergence from the contract's
/// <c>TimeoutException</c>.</para>
/// </summary>
public sealed class ConversionTimeoutException : Api2ConvertException
{
    public ConversionTimeoutException(Job job, int timeoutSeconds)
        : base($"Timed out after {timeoutSeconds}s waiting for job {job.Id} to finish "
            + $"(last status: {job.Status.Code}).")
    {
        Job = job;
    }

    /// <summary>The job as last observed before the timeout elapsed.</summary>
    public Job Job { get; }
}
