using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Api2Convert.Http;

namespace Api2Convert.Tests;

/// <summary>
/// A no-op <see cref="ISleeper"/> that records how long each retry/poll wait would have been, so tests
/// assert on backoff timing without actually sleeping.
/// </summary>
public sealed class RecordingSleeper : ISleeper
{
    public List<double> Slept { get; } = new();

    public Task SleepAsync(double seconds, CancellationToken cancellationToken)
    {
        Slept.Add(seconds);
        return Task.CompletedTask;
    }
}
