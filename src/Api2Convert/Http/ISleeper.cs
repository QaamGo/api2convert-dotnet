using System;
using System.Threading;
using System.Threading.Tasks;

namespace Api2Convert.Http;

/// <summary>
/// Pluggable delay used between retries and job polls. The default awaits a real
/// <see cref="Task.Delay(TimeSpan, CancellationToken)"/>; tests inject a no-op (or recording)
/// implementation so waits are instant and assertable.
/// </summary>
public interface ISleeper
{
    Task SleepAsync(double seconds, CancellationToken cancellationToken);
}

/// <summary>The production sleeper: awaits <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</summary>
public sealed class RealSleeper : ISleeper
{
    public Task SleepAsync(double seconds, CancellationToken cancellationToken)
    {
        if (seconds <= 0)
        {
            return Task.CompletedTask;
        }

        return Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
    }
}
