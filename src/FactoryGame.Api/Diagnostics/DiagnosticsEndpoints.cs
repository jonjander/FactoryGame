namespace FactoryGame.Api.Diagnostics;

public static class DiagnosticsEndpoints
{
    /// <summary>
    /// Plain-text log lines buffered since process start. No authentication (see configuration).
    /// </summary>
    public static void MapDiagnosticsEndpoints(this WebApplication app)
    {
        // Always register the route so it is never swallowed by MapFallbackToFile.
        // In Production the endpoint is disabled unless Diagnostics:ExposeRecentLogEndpoint=true.
        app.MapGet(
                "/diagnostics/recent-logs",
                (RecentLogBuffer buffer, IHostEnvironment env, IConfiguration config) =>
                {
                    if (!DiagnosticsLogEndpoint.IsEnabled(env, config))
                        return Results.Problem(
                            detail: "Set Diagnostics:ExposeRecentLogEndpoint=true to enable this endpoint in Production.",
                            statusCode: 403,
                            title: "Diagnostics endpoint is disabled.");

                    var body = string.Join(Environment.NewLine, buffer.Snapshot());
                    return Results.Text(body, "text/plain; charset=utf-8");
                })
            .WithName("DiagnosticsRecentLogs");
    }
}

public static class DiagnosticsLogEndpoint
{
    public static bool IsEnabled(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment()
        || configuration.GetValue("Diagnostics:ExposeRecentLogEndpoint", false);
}
