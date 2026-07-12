using System.Collections.Generic;
using System.Text;
using Api2Convert.Enums;
using Api2Convert.Support;

namespace Api2Convert.Models;

/// <summary>
/// A cloud-storage input descriptor — imports a file the API fetches from customer-owned storage
/// (S3, Azure Blob, FTP, Google Cloud Storage): <c>{ type:"cloud", source:&lt;provider&gt;, parameters,
/// credentials }</c>.
///
/// <para>Hand it to <c>client.ConvertAsync(cloudInput, ...)</c> / <c>StartConversionAsync(...)</c> as the
/// input, or to <c>client.Jobs.AddInputAsync(jobId, cloudInput.ToDescriptor())</c>; either way it emits
/// the wire descriptor via <see cref="ToDescriptor"/>. Like a remote URL, a cloud input is a
/// <strong>started</strong> job (<c>process = true</c>), not a staged upload.</para>
///
/// <para>The per-provider named constructors carry each provider's required keys <strong>verbatim</strong>
/// — flat and lowercase, exactly as the API expects (<c>accesskeyid</c>, not <c>access_key_id</c>). The
/// required keys are constructor arguments (structural correctness), <strong>not</strong> a runtime gate:
/// the builder never rejects a descriptor the permissive, asynchronously-validating server would accept.
/// Optional and forward-compat keys go through the trailing <c>parameters</c> / <c>credentials</c> maps,
/// or the generic <see cref="Of(CloudProvider, IReadOnlyDictionary{string, object?}, IReadOnlyDictionary{string, object?})"/>
/// escape hatch.</para>
///
/// <para>Google Drive <em>input</em> uses the <c>gdrive_picker</c> input type (the generic
/// <c>AddInputAsync</c> raw-map path this wave); <c>gdrive</c> / <c>youtube</c> are output-only.</para>
///
/// <para><c>credentials</c> ride in the plaintext body, so <see cref="ToString"/> masks the
/// <strong>whole</strong> credentials object to <c>[REDACTED]</c> and any sensitive <c>parameters</c>
/// leaf (see <see cref="Redactor"/>).</para>
/// </summary>
public sealed record CloudInput
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyMap = new Dictionary<string, object?>(0);

    /// <summary>Generic constructor: any provider (typed or a forward-compat string) with free-form maps.</summary>
    public CloudInput(
        string source,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IReadOnlyDictionary<string, object?>? credentials = null)
    {
        Source = source;
        Parameters = parameters ?? EmptyMap;
        Credentials = new CloudCredentials(credentials);
    }

    /// <summary>The provider, as a raw wire string (<c>amazons3</c>, <c>ftp</c>, ...).</summary>
    public string Source { get; init; }

    /// <summary>Non-secret locator keys (<c>bucket</c>, <c>file</c>, <c>host</c>, ...).</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; init; }

    /// <summary>Secret keys (access keys, passwords, tokens); never rendered.</summary>
    public CloudCredentials Credentials { get; init; }

    /// <summary>Generic escape hatch keyed by a typed provider.</summary>
    public static CloudInput Of(
        CloudProvider source,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IReadOnlyDictionary<string, object?>? credentials = null) =>
        new(source.Wire(), parameters, credentials);

    /// <summary>Generic escape hatch keyed by a raw provider string (e.g. a provider the enum doesn't know yet).</summary>
    public static CloudInput Of(
        string source,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IReadOnlyDictionary<string, object?>? credentials = null) =>
        new(source, parameters, credentials);

    /// <summary>Import from Amazon S3.</summary>
    public static CloudInput AmazonS3(
        string bucket,
        string file,
        string accesskeyid,
        string secretaccesskey,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IReadOnlyDictionary<string, object?>? credentials = null) =>
        new(
            CloudProvider.AmazonS3.Wire(),
            Merge(new() { ["bucket"] = bucket, ["file"] = file }, parameters),
            Merge(new() { ["accesskeyid"] = accesskeyid, ["secretaccesskey"] = secretaccesskey }, credentials));

    /// <summary>Import from Azure Blob Storage.</summary>
    public static CloudInput Azure(
        string container,
        string file,
        string accountname,
        string accountkey,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IReadOnlyDictionary<string, object?>? credentials = null) =>
        new(
            CloudProvider.Azure.Wire(),
            Merge(new() { ["container"] = container, ["file"] = file }, parameters),
            Merge(new() { ["accountname"] = accountname, ["accountkey"] = accountkey }, credentials));

    /// <summary>Import from an FTP server.</summary>
    public static CloudInput Ftp(
        string host,
        string file,
        string username,
        string password,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IReadOnlyDictionary<string, object?>? credentials = null) =>
        new(
            CloudProvider.Ftp.Wire(),
            Merge(new() { ["host"] = host, ["file"] = file }, parameters),
            Merge(new() { ["username"] = username, ["password"] = password }, credentials));

    /// <summary>Import from Google Cloud Storage.</summary>
    public static CloudInput GoogleCloud(
        string projectid,
        string bucket,
        string file,
        string keyfile,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IReadOnlyDictionary<string, object?>? credentials = null) =>
        new(
            CloudProvider.GoogleCloud.Wire(),
            Merge(new() { ["projectid"] = projectid, ["bucket"] = bucket, ["file"] = file }, parameters),
            Merge(new() { ["keyfile"] = keyfile }, credentials));

    /// <summary>
    /// The wire descriptor sent to <c>POST /jobs</c> (inline <c>input</c>) or
    /// <c>POST /jobs/{id}/input</c>: <c>{ type:"cloud", source, parameters, credentials }</c>.
    /// </summary>
    public IDictionary<string, object?> ToDescriptor() =>
        new Dictionary<string, object?>
        {
            ["type"] = InputType.Cloud.Wire(),
            ["source"] = Source,
            ["parameters"] = Parameters,
            ["credentials"] = Credentials.Values,
        };

    /// <summary>
    /// Human-readable form with credentials masked — safe to log. The whole <c>credentials</c> object
    /// renders as <c>[REDACTED]</c>; sensitive <c>parameters</c> leaves are masked too.
    /// </summary>
    public override string ToString() =>
        $"CloudInput(type=cloud, source={Source}, parameters={RenderParameters(Parameters)}, credentials={Redactor.Marker})";

    internal static string RenderParameters(IReadOnlyDictionary<string, object?> parameters) =>
        Encoding.UTF8.GetString(Json.Encode(Redactor.MaskParameters(parameters)));

    private static Dictionary<string, object?> Merge(
        Dictionary<string, object?> required,
        IReadOnlyDictionary<string, object?>? extra)
    {
        if (extra is not null)
        {
            foreach (KeyValuePair<string, object?> entry in extra)
            {
                required[entry.Key] = entry.Value;
            }
        }

        return required;
    }
}
