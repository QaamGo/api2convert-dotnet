using System;

namespace Api2Convert.Http;

/// <summary>
/// Immutable client configuration (the tuning knobs). Build via <see cref="Builder"/> so you only set
/// what you need; everything has a sensible default, and <see cref="Builder.Build"/> clamps the poll
/// knobs so no configuration can busy-loop or poll unbounded.
/// </summary>
public sealed class Config
{
    public const string DefaultBaseUrl = "https://api.api2convert.com/v2";

    /// <summary>
    /// Hard floor for the job-poll interval. A caller-supplied 0 or negative value is raised to this
    /// so the poll loop can never busy-spin the API.
    /// </summary>
    public const double MinPollInterval = 0.5;

    /// <summary>
    /// Hard ceiling for the total job-poll timeout (4 hours). A misconfigured or hostile-large timeout
    /// degrades to a bounded wait instead of an unbounded poll.
    /// </summary>
    public const int MaxPollTimeout = 14400;

    private Config(
        string baseUrl,
        int timeoutSeconds,
        int maxRetries,
        double pollInterval,
        double pollMaxInterval,
        int pollTimeoutSeconds)
    {
        BaseUrl = baseUrl;
        TimeoutSeconds = timeoutSeconds;
        MaxRetries = maxRetries;
        PollInterval = pollInterval;
        PollMaxInterval = pollMaxInterval;
        PollTimeoutSeconds = pollTimeoutSeconds;
    }

    /// <summary>API base URL, e.g. <c>https://api.api2convert.com/v2</c> (no trailing slash).</summary>
    public string BaseUrl { get; }

    /// <summary>Per-request network timeout, in seconds (never below 1).</summary>
    public int TimeoutSeconds { get; }

    /// <summary>Automatic retries for transient failures (429 / 5xx / network).</summary>
    public int MaxRetries { get; }

    /// <summary>First poll interval when waiting for a job, in seconds (never below <see cref="MinPollInterval"/>).</summary>
    public double PollInterval { get; }

    /// <summary>Upper bound the poll interval backs off to, in seconds (never below <see cref="PollInterval"/>).</summary>
    public double PollMaxInterval { get; }

    /// <summary>How long to wait for a job to finish before giving up, in seconds (capped at <see cref="MaxPollTimeout"/>).</summary>
    public int PollTimeoutSeconds { get; }

    /// <summary>The default configuration (all knobs at their defaults).</summary>
    public static Config Default { get; } = new Builder().Build();

    /// <summary>Mutable, fluent builder; <see cref="Build"/> is the single clamping entry point.</summary>
    public sealed class Builder
    {
        private string _baseUrl = DefaultBaseUrl;
        private int _timeoutSeconds = 30;
        private int _maxRetries = 2;
        private double _pollInterval = 1.0;
        private double _pollMaxInterval = 5.0;
        private int _pollTimeoutSeconds = 300;

        public Builder BaseUrl(string? baseUrl)
        {
            if (!string.IsNullOrEmpty(baseUrl))
            {
                _baseUrl = baseUrl;
            }

            return this;
        }

        public Builder Timeout(int seconds)
        {
            _timeoutSeconds = seconds;
            return this;
        }

        public Builder MaxRetries(int retries)
        {
            _maxRetries = retries;
            return this;
        }

        public Builder PollInterval(double seconds)
        {
            _pollInterval = seconds;
            return this;
        }

        public Builder PollMaxInterval(double seconds)
        {
            _pollMaxInterval = seconds;
            return this;
        }

        public Builder PollTimeout(int seconds)
        {
            _pollTimeoutSeconds = seconds;
            return this;
        }

        public Config Build()
        {
            // Clamp so a caller value can neither busy-loop (interval floor) nor poll unbounded
            // (timeout ceiling), the max interval is never below the starting interval, and the
            // per-request timeout is never disabled (0 = "no timeout" is an unbounded-hang landmine).
            double interval = Math.Max(MinPollInterval, _pollInterval);
            double maxInterval = Math.Max(interval, _pollMaxInterval);
            int totalTimeout = Math.Min(MaxPollTimeout, Math.Max(0, _pollTimeoutSeconds));
            string trimmedBaseUrl = _baseUrl.EndsWith('/')
                ? _baseUrl[..^1]
                : _baseUrl;

            return new Config(
                trimmedBaseUrl,
                Math.Max(1, _timeoutSeconds),
                Math.Max(0, _maxRetries),
                interval,
                maxInterval,
                totalTimeout);
        }
    }
}
