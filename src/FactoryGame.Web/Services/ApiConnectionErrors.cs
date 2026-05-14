using System.Net.Http;

namespace FactoryGame.Web.Services;

public static class ApiConnectionErrors
{
    public static string Format(Exception ex)
    {
        var technical = DeepestMessage(ex);
        if (LooksLikeTransportFailure(ex, technical))
        {
            return "Kan inte ansluta till API:et. Starta FactoryGame.Api (t.ex. dotnet run --project src/FactoryGame.Api) "
                + "eller sätt ApiBaseUrl i wwwroot/factory-config.json. Vid separat Blazor dev-server används standard "
                + "https://localhost:7145 från appsettings.Development.json om nyckeln ApiBaseUrl inte sätts i factory-config. "
                + "Teknik: " + technical;
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
