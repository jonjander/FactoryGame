using System.Net.Http;

namespace FactoryGame.Web.Services;

public static class ApiConnectionErrors
{
    public static string Format(Exception ex, Uri? httpClientBaseAddress = null)
    {
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

    private static bool LooksLikeTransportFailure(Exception ex, string text)
    {
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
