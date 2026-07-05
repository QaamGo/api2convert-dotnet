using System.Threading;
using System.Threading.Tasks;
using Api2Convert.Http;

namespace Api2Convert.Resources;

/// <summary>
/// API usage statistics. The response shape is free-form, so these return the decoded body as-is
/// (a dictionary or a list).
///
/// <para><c>filter</c> is either an API key to scope to, or <c>all</c>.</para>
/// </summary>
public sealed class StatsResource
{
    private readonly Transport _transport;

    public StatsResource(Transport transport) => _transport = transport;

    /// <param name="day">format <c>yyyy-mm-dd</c>.</param>
    public Task<object?> DayAsync(string day, string filter = "all", CancellationToken cancellationToken = default) =>
        _transport.RequestAsync("GET", $"/stats/day/{day}/{filter}", cancellationToken: cancellationToken);

    /// <param name="month">format <c>yyyy-mm</c>.</param>
    public Task<object?> MonthAsync(string month, string filter = "all", CancellationToken cancellationToken = default) =>
        _transport.RequestAsync("GET", $"/stats/month/{month}/{filter}", cancellationToken: cancellationToken);

    /// <param name="year">format <c>yyyy</c>.</param>
    public Task<object?> YearAsync(string year, string filter = "all", CancellationToken cancellationToken = default) =>
        _transport.RequestAsync("GET", $"/stats/year/{year}/{filter}", cancellationToken: cancellationToken);
}
