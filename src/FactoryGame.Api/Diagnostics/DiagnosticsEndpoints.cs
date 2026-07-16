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

        app.MapPost(
                "/diagnostics/client-log",
                (ClientLogRequest request, ClientLogBuffer buffer, IHostEnvironment env, IConfiguration config) =>
                {
                    if (!DiagnosticsLogEndpoint.IsEnabled(env, config))
                        return Results.Problem(
                            detail: "Client diagnostics are disabled outside Development unless Diagnostics:ExposeRecentLogEndpoint=true.",
                            statusCode: 403,
                            title: "Diagnostics endpoint is disabled.");

                    var ts = string.IsNullOrWhiteSpace(request.Ts)
                        ? DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")
                        : request.Ts.Trim();
                    var level = string.IsNullOrWhiteSpace(request.Level) ? "Info" : request.Level.Trim();
                    var message = request.Message?.Trim() ?? string.Empty;
                    if (message.Length == 0)
                        return Results.BadRequest();

                    var line = $"{ts} [{level}] {message}";
                    if (!string.IsNullOrWhiteSpace(request.Url))
                        line += $" (url={request.Url.Trim()})";
                    if (!string.IsNullOrWhiteSpace(request.Source))
                        line += $" (source={request.Source.Trim()})";

                    buffer.AddLine(line);
                    return Results.NoContent();
                })
            .WithName("DiagnosticsClientLog");

        app.MapGet(
                "/diagnostics/client-logs",
                (ClientLogBuffer buffer, IHostEnvironment env, IConfiguration config) =>
                {
                    if (!DiagnosticsLogEndpoint.IsEnabled(env, config))
                        return Results.Problem(
                            detail: "Client diagnostics are disabled outside Development unless Diagnostics:ExposeRecentLogEndpoint=true.",
                            statusCode: 403,
                            title: "Diagnostics endpoint is disabled.");

                    var body = string.Join(Environment.NewLine, buffer.Snapshot());
                    return Results.Text(body, "text/plain; charset=utf-8");
                })
            .WithName("DiagnosticsClientLogs");
    }
}

public sealed record ClientLogRequest(string? Message, string? Level = null, string? Ts = null, string? Url = null, string? Source = null);

public static class DiagnosticsLogEndpoint
{
    public static bool IsEnabled(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment()
        || environment.IsEnvironment("Testing")
        || configuration.GetValue("Diagnostics:ExposeRecentLogEndpoint", false);
}
