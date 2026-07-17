using System.Net;
using System.Net.Http;

namespace FactoryGame.Web.Services;

public static class ApiConnectionErrors
{
    public static string Format(Exception ex, Uri? httpClientBaseAddress = null)
    {
        if (TryFormatUnauthorized(ex, out var unauthorized))
            return unauthorized;

        var technical = DeepestMessage(ex);
        if (LooksLikeTransportFailure(ex, technical))
        {
            var baseHint = httpClientBaseAddress != null
                ? $" HttpClient.BaseAddress: {httpClientBaseAddress}"
                : "";
            return "Cannot connect to the API."
                + baseHint
                + " Azure: check deploy and that `ASPNETCORE_ENVIRONMENT` is Production (Release build). "
                + "Local: start the API (`dotnet run --project src/FactoryGame.Api`) or pick the VS profile "
                + "«https (UI -> Azure API)» / «https (UI -> local API)». See `factory-config.json` (ApiTarget) and README. "
                + "Clear site data / service worker if errors persist after deploy. Technical: " + technical;
        }

        return string.IsNullOrWhiteSpace(ex.Message) ? technical : ex.Message;
    }

    private static string DeepestMessage(Exception ex)
    {
        var e = ex;
        while (e.InnerException != null)
            e = e.InnerException;
        return string.IsNullOrWhiteSpace(e.Message) ? ex.GetType().Name : e.Message;
    }

    private static bool TryFormatUnauthorized(Exception ex, out string message)
    {
        message = "";
        if (FindHttpStatusCode(ex) == HttpStatusCode.Unauthorized)
        {
            message = "Session is invalid or expired (common after a server update). "
                + "Click Log out in the menu and sign in as guest again.";
            return true;
        }

        var text = DeepestMessage(ex);
        if (text.Contains("401", StringComparison.Ordinal) &&
            text.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            message = "Session is invalid or expired. Log out and sign in as guest again.";
            return true;
        }

        return false;
    }

    private static HttpStatusCode? FindHttpStatusCode(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is HttpRequestException { StatusCode: { } code })
                return code;
        }

        return null;
    }

    private static bool LooksLikeTransportFailure(Exception ex, string text)
    {
        if (ex is HttpRequestException { StatusCode: { } code })
        {
            if (code is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return false;
            if ((int)code >= 500)
                return false;
        }

        if (ex is HttpRequestException hre && hre.StatusCode is null &&
            text.Contains("401", StringComparison.Ordinal))
            return false;

        if (ex is HttpRequestException { StatusCode: null })
            return true;
        if (ex is HttpRequestException { StatusCode: { } c } && (int)c < 500)
            return true;
        if (text.Contains("Load failed", StringComparison.OrdinalIgnoreCase))
            return true;
        if (text.Contains("Failed to fetch", StringComparison.OrdinalIgnoreCase))
            return true;
        if (text.Contains("NetworkError", StringComparison.OrdinalIgnoreCase))
            return true;
        return ex.InnerException != null && LooksLikeTransportFailure(ex.InnerException, DeepestMessage(ex.InnerException));
    }
}
