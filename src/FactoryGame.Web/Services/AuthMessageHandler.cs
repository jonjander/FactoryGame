using System.Net;
using System.Net.Http.Headers;

namespace FactoryGame.Web.Services;

public sealed class AuthMessageHandler(TokenStore tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var hadToken = !string.IsNullOrEmpty(tokens.BearerToken);
        if (hadToken)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.BearerToken);

        var response = await base.SendAsync(request, cancellationToken);

        if (hadToken && response.StatusCode == HttpStatusCode.Unauthorized)
            await tokens.SetTokenAsync(null, cancellationToken);

        return response;
    }
}
