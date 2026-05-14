using System.Reflection;
using System.Threading.RateLimiting;
using FactoryGame.Api.Auth;
using FactoryGame.Api.Diagnostics;
using FactoryGame.Api.Endpoints;
using FactoryGame.Infrastructure;
using FactoryGame.Infrastructure.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

var logMaxLines = builder.Configuration.GetValue<int?>("Diagnostics:RecentLogMaxLines") ?? 4000;
var logBuffer = new RecentLogBuffer(logMaxLines);
builder.Services.AddSingleton(logBuffer);
var bufferedMinLevel = builder.Configuration.GetValue("Diagnostics:MinimumBufferedLogLevel", LogLevel.Information);
builder.Logging.AddProvider(new RecentLogBufferLoggerProvider(logBuffer, bufferedMinLevel));

if (!builder.Environment.IsDevelopment())
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var apiVersion = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(Program).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "FactoryGame API", Version = apiVersion });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Session token from POST /v1/auth/guest",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "opaque"
    });
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "Server-issued API key",
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        [new OpenApiSecuritySchemeReference("ApiKey", document)] = []
    });
});

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o =>
{
    o.AddPolicy("wasm", p =>
    {
        if (corsOrigins.Length == 0)
            p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        else
            p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var path = ctx.Request.Path;
        if (!path.StartsWithSegments("/v1"))
            return RateLimitPartition.GetNoLimiter("nonapi");

        var key = ctx.Items["PlayerId"] is Guid pid
            ? $"p:{pid}"
            : $"ip:{ctx.Connection.RemoteIpAddress?.ToString() ?? "anon"}";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 600,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseForwardedHeaders();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var schema = scope.ServiceProvider.GetRequiredService<IDatabaseSchemaInitializer>();
    await schema.EnsureSchemaAsync(db);
}

app.UseHttpsRedirection();

app.UseCors("wasm");

// Serve Blazor PWA before Swagger so GET / resolves to index.html, not Swagger UI.
app.UseBlazorFrameworkFiles();
app.UseDefaultFiles();
app.UseStaticFiles();

// Only run Swagger generators/UI for /swagger* so no middleware in that stack can intercept SPA routes (e.g. GET /).
app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase),
    branch =>
    {
        branch.UseSwagger();
        branch.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "FactoryGame v1");
        });
    });

app.UseRateLimiter();

app.UseMiddleware<PlayerSessionMiddleware>();

app.MapHealthChecks("/health").WithName("Health");

app.MapDiagnosticsEndpoints();

app.MapAuthEndpoints();
app.MapPlayerEndpoints();
app.MapContentEndpoints();
app.MapMarketEndpoints();
app.MapBoardEndpoints();
app.MapAdminEndpoints();

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
