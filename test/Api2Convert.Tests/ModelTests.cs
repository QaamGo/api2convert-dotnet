using System.Collections.Generic;
using Api2Convert.Enums;
using Api2Convert.Models;
using Api2Convert.Support;
using Xunit;

namespace Api2Convert.Tests;

public sealed class ModelTests
{
    [Fact]
    public void JobHydratesDefensivelyAndExposesPredicates()
    {
        var job = Job.FromDict(A2CTestBase_Parse(
            "{\"id\":\"job-9\",\"status\":{\"code\":\"completed\",\"info\":\"done\"},"
            + "\"output\":[{\"id\":\"o1\",\"uri\":\"https://dl/x\",\"filename\":\"x.png\",\"size\":1234}],"
            + "\"surprise_field\":[1,2,3]}"));

        Assert.Equal("job-9", job.Id);
        Assert.True(job.IsCompleted);
        Assert.True(job.IsTerminal);
        Assert.False(job.IsFailed);
        Assert.Single(job.Output);
        Assert.Equal(1234L, job.Output[0].Size);

        // Unknown fields are tolerated and preserved in Raw (forward-compat).
        Assert.True(job.Raw.ContainsKey("surprise_field"));
    }

    [Fact]
    public void MissingAndWrongTypedFieldsNeverThrowDuringHydration()
    {
        // status is the wrong type, size is a string, no output at all — hydration must not throw.
        var job = Job.FromDict(A2CTestBase_Parse("{\"id\":123,\"status\":\"oops\",\"output\":\"nope\"}"));

        Assert.Equal("", job.Id); // id was a number, not a string -> default ""
        Assert.Equal("", job.Status.Code);
        Assert.Empty(job.Output);
        Assert.False(job.IsTerminal);
    }

    [Fact]
    public void UnknownStatusCodesAreNonTerminal()
    {
        Assert.False(JobStatuses.IsTerminalCode("some-future-code"));
        Assert.True(JobStatuses.IsTerminalCode("completed"));
        Assert.True(JobStatuses.IsTerminalCode("failed"));
        Assert.True(JobStatuses.IsTerminalCode("canceled"));
        Assert.False(JobStatuses.IsTerminalCode("processing"));
    }

    [Fact]
    public void EnumWireMappingRoundTrips()
    {
        Assert.Equal("input_id", InputType.InputId.Wire());
        Assert.Equal(InputType.Remote, InputTypes.FromWire("remote"));
        Assert.Null(InputTypes.FromWire("unknown"));
        Assert.Equal("completed", JobStatus.Completed.Wire());
        Assert.Equal(JobStatus.Failed, JobStatuses.FromWire("failed"));
    }

    [Fact]
    public void DataNullableLongAcceptsNumbersAndStringsButRejectsBooleans()
    {
        Assert.Equal(42L, Data.NullableLong(42L));
        Assert.Equal(42L, Data.NullableLong("42"));
        Assert.Equal(42L, Data.NullableLong(42.9));
        Assert.Null(Data.NullableLong(true));
        Assert.Null(Data.NullableLong("not-a-number"));
        Assert.Null(Data.NullableLong(null));
    }

    // Local JSON parse helper (ModelTests does not extend A2CTestBase).
    private static IReadOnlyDictionary<string, object?> A2CTestBase_Parse(string json) =>
        (IReadOnlyDictionary<string, object?>)Json.Decode(System.Text.Encoding.UTF8.GetBytes(json))!;
}
