using System.Threading;
using System.Threading.Tasks;

namespace Api2Convert.Http;

/// <summary>
/// The pluggable HTTP transport seam. The default is <see cref="HttpClientSender"/> (backed by
/// <see cref="System.Net.Http.HttpClient"/>); tests inject a fake that serves canned responses.
///
/// <para>An implementation must honor <see cref="HttpRequest.FollowRedirects"/> <em>per request</em>:
/// never follow a redirect on a request carrying an <c>X-Oc-*</c> secret header (that would forward
/// the secret to the redirect target), and follow redirects only on the self-contained download
/// path. Retry/backoff and error mapping live in the transport, not here — an implementation should
/// surface a transport failure as an <see cref="Exceptions.NetworkException"/> (or let one bubble up).</para>
/// </summary>
public interface IHttpSender
{
    Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken cancellationToken);
}
