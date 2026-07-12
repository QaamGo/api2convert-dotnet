using System.Collections.Generic;
using System.Threading.Tasks;
using Api2Convert.Enums;
using Api2Convert.Models;
using Xunit;

namespace Api2Convert.Tests;

/// <summary>
/// Cloud-connector parity fixtures 1 (create-payload serialization) and 2 (read hydration), plus the
/// unit behaviour of the new cloud types. The JSON shapes and assertions mirror the canonical fixtures
/// shared across every SDK.
/// </summary>
public sealed class CloudConnectorTests : A2CTestBase
{
    // ---- Fixture 1: create-payload (what ConvertAsync serializes) --------------------------------

    [Fact]
    public async Task Fixture1_ConvertSerializesCloudInputAndOutputTarget()
    {
        // create -> started job; WaitAsync polls once to a completed job with no local output.
        Http.AddJson(201, "{\"id\":\"job-1\",\"status\":{\"code\":\"incomplete\"}}");
        Http.AddJson(200, "{\"id\":\"job-1\",\"status\":{\"code\":\"completed\"}}");

        CloudInput input = CloudInput.AmazonS3("my-bucket", "in/photo.png", "AKIA_TEST", "SECRET_TEST");
        var target = new OutputTarget(
            "ftp",
            new Dictionary<string, object?> { ["host"] = "ftp.example.com", ["file"] = "/out/photo.jpg" },
            new Dictionary<string, object?> { ["username"] = "u", ["password"] = "p" });

        await Client().ConvertAsync(input, "jpg", opts: new ConvertOptions { OutputTargets = new[] { target } });

        IReadOnlyDictionary<string, object?> body = RequestAt(0).BodyJson();

        // 1) a cloud input is a started job (like a remote URL), not staged/uploaded.
        Assert.Equal(true, body["process"]);

        // 2) input[0] carries the flat/lowercase keys exactly as the factory emits them.
        IReadOnlyDictionary<string, object?> inputDescriptor = FirstOf(body["input"]);
        Assert.Equal("cloud", inputDescriptor["type"]);
        Assert.Equal("amazons3", inputDescriptor["source"]);
        AssertMap(new Dictionary<string, object?> { ["bucket"] = "my-bucket", ["file"] = "in/photo.png" }, inputDescriptor["parameters"]);
        AssertMap(new Dictionary<string, object?> { ["accesskeyid"] = "AKIA_TEST", ["secretaccesskey"] = "SECRET_TEST" }, inputDescriptor["credentials"]);

        // 3) conversion[0].output_target[0] serializes {type,parameters,credentials} and NO status.
        IReadOnlyDictionary<string, object?> conversion = FirstOf(body["conversion"]);
        IReadOnlyDictionary<string, object?> outputTarget = FirstOf(conversion["output_target"]);
        Assert.Equal("ftp", outputTarget["type"]);
        AssertMap(new Dictionary<string, object?> { ["host"] = "ftp.example.com", ["file"] = "/out/photo.jpg" }, outputTarget["parameters"]);
        AssertMap(new Dictionary<string, object?> { ["username"] = "u", ["password"] = "p" }, outputTarget["credentials"]);
        Assert.False(outputTarget.ContainsKey("status"));

        // output targets never leak into the conversion options map.
        Assert.False(conversion.ContainsKey("options"));
    }

    [Fact]
    public async Task Fixture1_RawCreatePathProducesByteIdenticalOutputTarget()
    {
        Http.AddJson(201, "{\"id\":\"job-1\",\"status\":{\"code\":\"completed\"}}");

        var payload = new Dictionary<string, object?>
        {
            ["process"] = true,
            ["input"] = new List<object?> { CloudInput.AmazonS3("my-bucket", "in/photo.png", "AKIA_TEST", "SECRET_TEST").ToDescriptor() },
            ["conversion"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["target"] = "jpg",
                    ["output_target"] = new List<object?>
                    {
                        OutputTarget.Of(
                            CloudProvider.Ftp,
                            new Dictionary<string, object?> { ["host"] = "ftp.example.com", ["file"] = "/out/photo.jpg" },
                            new Dictionary<string, object?> { ["username"] = "u", ["password"] = "p" }).ToDescriptor(),
                    },
                },
            },
        };

        await Client().Jobs.CreateAsync(payload);

        IReadOnlyDictionary<string, object?> body = RequestAt(0).BodyJson();

        // Both the ConvertAsync OutputTargets control and the raw create map yield the same bytes.
        IReadOnlyDictionary<string, object?> inputDescriptor = FirstOf(body["input"]);
        Assert.Equal("amazons3", inputDescriptor["source"]);
        AssertMap(new Dictionary<string, object?> { ["accesskeyid"] = "AKIA_TEST", ["secretaccesskey"] = "SECRET_TEST" }, inputDescriptor["credentials"]);

        IReadOnlyDictionary<string, object?> outputTarget = FirstOf(FirstOf(body["conversion"])["output_target"]);
        Assert.Equal("ftp", outputTarget["type"]);
        Assert.False(outputTarget.ContainsKey("status"));
        AssertMap(new Dictionary<string, object?> { ["username"] = "u", ["password"] = "p" }, outputTarget["credentials"]);
    }

    [Fact]
    public async Task AddInputAcceptsCloudInputDescriptor()
    {
        Http.AddJson(200, "{\"id\":\"in-1\",\"type\":\"cloud\",\"source\":\"ftp\"}");

        await Client().Jobs.AddInputAsync("job-1", CloudInput.Ftp("ftp.example.com", "in/a.png", "u", "p").ToDescriptor());

        IReadOnlyDictionary<string, object?> body = RequestAt(0).BodyJson();
        Assert.Equal("cloud", body["type"]);
        Assert.Equal("ftp", body["source"]);
        AssertMap(new Dictionary<string, object?> { ["host"] = "ftp.example.com", ["file"] = "in/a.png" }, body["parameters"]);
        AssertMap(new Dictionary<string, object?> { ["username"] = "u", ["password"] = "p" }, body["credentials"]);
    }

    // ---- Fixture 2: read hydration (a GET /jobs/{id} response) -----------------------------------

    [Fact]
    public void Fixture2_HydratesCloudInputAndOutputTarget()
    {
        var job = Models.Job.FromDict(Parse(
            "{\"id\":\"job-1\",\"status\":{\"code\":\"completed\"},"
            + "\"input\":[{\"id\":\"in-1\",\"type\":\"cloud\",\"source\":\"amazons3\",\"status\":\"ready\","
            + "\"parameters\":{\"bucket\":\"my-bucket\",\"file\":\"in/photo.png\"},\"credentials\":{}}],"
            + "\"conversion\":[{\"id\":\"c-1\",\"target\":\"jpg\",\"output_target\":[{\"type\":\"ftp\","
            + "\"parameters\":{\"host\":\"ftp.example.com\",\"file\":\"/out/photo.jpg\"},"
            + "\"credentials\":{},\"status\":\"uploading\"}]}]}"));

        // 1) input source is a RAW string; parameters surface.
        InputFile input = job.Input[0];
        Assert.Equal("amazons3", input.Source);
        Assert.Equal("ready", input.Status);
        Assert.Equal("my-bucket", input.Parameters["bucket"]);
        Assert.Equal("in/photo.png", input.Parameters["file"]);

        // 2) output target status/parameters/type surface.
        OutputTarget output = job.Conversion[0].OutputTargets[0];
        Assert.Equal("ftp", output.Type);
        Assert.Equal("uploading", output.Status);
        Assert.Equal("ftp.example.com", output.Parameters["host"]);

        // 3) credentials are never surfaced (the API returns them empty; the SDK does not hydrate).
        Assert.Equal(0, output.Credentials.Count);
    }

    [Fact]
    public void Fixture2_UnknownProviderRoundTripsUntyped()
    {
        var job = Models.Job.FromDict(Parse(
            "{\"id\":\"job-1\",\"status\":{\"code\":\"completed\"},"
            + "\"input\":[{\"id\":\"in-1\",\"type\":\"cloud\",\"source\":\"r2\",\"status\":\"ready\"}],"
            + "\"conversion\":[{\"target\":\"jpg\",\"output_target\":[{\"type\":\"r2\",\"status\":\"waiting\"}]}]}"));

        // An unknown provider string hydrates without any enum parse throwing.
        Assert.Equal("r2", job.Input[0].Source);
        Assert.Equal("r2", job.Conversion[0].OutputTargets[0].Type);
        Assert.Equal("waiting", job.Conversion[0].OutputTargets[0].Status);
        Assert.Null(CloudProviders.FromWire("r2"));
    }

    // ---- Unit: the new value types --------------------------------------------------------------

    [Fact]
    public void CloudProviderVocabulary()
    {
        Assert.Equal("amazons3", CloudProvider.AmazonS3.Wire());
        Assert.Equal("azure", CloudProvider.Azure.Wire());
        Assert.Equal("ftp", CloudProvider.Ftp.Wire());
        Assert.Equal("gdrive", CloudProvider.Gdrive.Wire());
        Assert.Equal("googlecloud", CloudProvider.GoogleCloud.Wire());
        Assert.Equal("youtube", CloudProvider.Youtube.Wire());
        // Tolerant resolution never throws on an unknown provider.
        Assert.Null(CloudProviders.FromWire("r2"));
    }

    [Fact]
    public void PerProviderConstructorsCarryRequiredKeysVerbatim()
    {
        IDictionary<string, object?> azure = CloudInput.Azure("c", "f", "n", "k").ToDescriptor();
        Assert.Equal("azure", azure["source"]);
        AssertMap(new Dictionary<string, object?> { ["container"] = "c", ["file"] = "f" }, azure["parameters"]);
        AssertMap(new Dictionary<string, object?> { ["accountname"] = "n", ["accountkey"] = "k" }, azure["credentials"]);

        IDictionary<string, object?> gcs = CloudInput.GoogleCloud("p", "b", "f", "kf").ToDescriptor();
        Assert.Equal("googlecloud", gcs["source"]);
        AssertMap(new Dictionary<string, object?> { ["projectid"] = "p", ["bucket"] = "b", ["file"] = "f" }, gcs["parameters"]);
        AssertMap(new Dictionary<string, object?> { ["keyfile"] = "kf" }, gcs["credentials"]);
    }

    [Fact]
    public void GenericEscapeHatchCarriesForwardCompatKeys()
    {
        CloudInput input = CloudInput.AmazonS3(
            "b",
            "f",
            "id",
            "sec",
            new Dictionary<string, object?> { ["region"] = "eu" },
            new Dictionary<string, object?> { ["sessiontoken"] = "t" });

        AssertMap(new Dictionary<string, object?> { ["bucket"] = "b", ["file"] = "f", ["region"] = "eu" }, input.Parameters);
        AssertMap(
            new Dictionary<string, object?> { ["accesskeyid"] = "id", ["secretaccesskey"] = "sec", ["sessiontoken"] = "t" },
            input.Credentials.Values);
    }

    [Fact]
    public void OutputTargetOmitsStatusOnSerializeButHydratesItOnRead()
    {
        var created = new OutputTarget(
            "ftp",
            new Dictionary<string, object?> { ["host"] = "h" },
            new Dictionary<string, object?> { ["username"] = "u" },
            status: "completed");
        Assert.False(created.ToDescriptor().ContainsKey("status"));

        OutputTarget read = OutputTarget.FromDict(Parse("{\"type\":\"ftp\",\"parameters\":{\"host\":\"h\"},\"status\":\"completed\"}"));
        Assert.Equal("completed", read.Status);
        Assert.Equal(0, read.Credentials.Count);
    }

    private static IReadOnlyDictionary<string, object?> FirstOf(object? value)
    {
        var list = Assert.IsAssignableFrom<IReadOnlyList<object?>>(value);
        return Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(list[0]);
    }

    private static void AssertMap(IReadOnlyDictionary<string, object?> expected, object? actual)
    {
        var map = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(actual);
        Assert.Equal(expected.Count, map.Count);
        foreach (KeyValuePair<string, object?> entry in expected)
        {
            Assert.True(map.TryGetValue(entry.Key, out object? value), $"missing key {entry.Key}");
            Assert.Equal(entry.Value, value);
        }
    }
}
