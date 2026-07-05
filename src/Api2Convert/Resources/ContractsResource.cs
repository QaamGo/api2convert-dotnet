using System.Threading;
using System.Threading.Tasks;
using Api2Convert.Http;

namespace Api2Convert.Resources;

/// <summary>
/// Information about the account's active contracts. Free-form response, returned as the decoded
/// body (a dictionary or a list).
/// </summary>
public sealed class ContractsResource
{
    private readonly Transport _transport;

    public ContractsResource(Transport transport) => _transport = transport;

    public Task<object?> GetAsync(CancellationToken cancellationToken = default) =>
        _transport.RequestAsync("GET", "/contracts", cancellationToken: cancellationToken);
}
