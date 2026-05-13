using System.Net.Http.Headers;

namespace FactoryGame.Web.Services;

public sealed class AuthMessageHandler(TokenStore tokens) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(tokens.BearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.BearerToken);
        return base.SendAsync(request, cancellationToken);
    }
}
