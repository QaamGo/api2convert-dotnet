using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Api2Convert.Http;
using Xunit;

namespace Api2Convert.Tests;

/// <summary>
/// The in-memory sender must honor cancellation like a real transport, so tests that assert a
/// cancelled token aborts a call are meaningful rather than silently passing.
/// </summary>
public sealed class FakeHttpSenderTests
{
    [Fact]
    public async Task APreCancelledTokenAbortsTheSend()
    {
        var sender = new FakeHttpSender();
        sender.AddJson(200, "{}");
        var cancelled = new CancellationToken(canceled: true);

        HttpRequest request = HttpRequest.Of(
            "GET",
            "https://example.test/x",
            new Dictionary<string, string>(),
            null,
            false);

        await Assert.ThrowsAnyAsync<System.OperationCanceledException>(
            () => sender.SendAsync(request, cancelled));

        // The cancellation must be observed before the canned response is dequeued.
        Assert.Empty(sender.Requests);
    }
}
