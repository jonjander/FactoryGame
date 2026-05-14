namespace FactoryGame.Api.Diagnostics;

public static class DiagnosticsEndpoints
{
    /// <summary>
    /// Plain-text log lines buffered since process start. No authentication (see configuration).
    /// </summary>
    public static void MapDiagnosticsEndpoints(this WebApplication app)
    {
        if (!DiagnosticsLogEndpoint.IsEnabled(app.Environment, app.Configuration))
            return;

        app.MapGet(
                "/diagnostics/recent-logs",
                (RecentLogBuffer buffer) =>
                {
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
