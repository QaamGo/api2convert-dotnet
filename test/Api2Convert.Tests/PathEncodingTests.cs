using System.Threading.Tasks;
using Xunit;

namespace Api2Convert.Tests;

/// <summary>
/// Dynamic path segments (job id, preset id, stats date/filter) must be percent-encoded so a value
/// containing <c>/</c>, <c>?</c> or <c>#</c> cannot alter the request path or inject a query/fragment.
/// Query parameters are encoded separately by the transport.
/// </summary>
public sealed class PathEncodingTests : A2CTestBase
{
    [Fact]
    public async Task JobIdIsPercentEncodedIntoThePathSegment()
    {
        Http.AddJson(200, "{\"id\":\"x\",\"status\":{\"code\":\"completed\"}}");

        await Client().Jobs.GetAsync("a b/c?d#e");

        Assert.EndsWith("/jobs/a%20b%2Fc%3Fd%23e", RequestAt(0).Uri);
    }

    [Fact]
    public async Task PresetIdIsPercentEncodedIntoThePathSegment()
    {
        Http.AddJson(200, "{\"id\":\"x\"}");

        await Client().Presets.GetAsync("p/../secret");

        Assert.EndsWith("/presets/p%2F..%2Fsecret", RequestAt(0).Uri);
    }

    [Fact]
    public async Task StatsDateAndFilterAreEncodedIntoTheirSegments()
    {
        Http.AddJson(200, "{}");

        await Client().Stats.DayAsync("2026-01-01", "team/one");

        Assert.EndsWith("/stats/day/2026-01-01/team%2Fone", RequestAt(0).Uri);
    }
}
