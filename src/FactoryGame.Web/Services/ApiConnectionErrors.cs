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
            return "Kan inte ansluta till API:et. I Azure: kontrollera att App Service kör rätt build, att "
                + "`ASPNETCORE_ENVIRONMENT` är `Production` om du inte avsiktligt kör Development, och att `factory-config.json` "
                + "inte pekar ApiBaseUrl mot localhost. Rensa webbplatsdata / avregistrera service worker om felet kvarstår efter deploy. "
                + "Vid lokal Blazor dev-server används automatiskt https://localhost:7145 för kända dev-portar. Teknik: " + technical
                + baseHint;
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
            message = "Sessionen är ogiltig eller har gått ut (vanligt efter server-uppdatering). "
                + "Klicka Logga ut i menyn och logga in som gäst igen.";
            return true;
        }

        var text = DeepestMessage(ex);
        if (text.Contains("401", StringComparison.Ordinal) &&
            text.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            message = "Sessionen är ogiltig eller har gått ut. Logga ut och logga in som gäst igen.";
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
        if (ex is HttpRequestException { StatusCode: { } code } &&
            code is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return false;

        if (ex is HttpRequestException hre && hre.StatusCode is null &&
            text.Contains("401", StringComparison.Ordinal))
            return false;

        if (ex is HttpRequestException)
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
