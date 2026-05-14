using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace FactoryGame.Web.Services;

public sealed class OfflineCommandQueue(BrowserStorage storage, IHttpClientFactory httpFactory)
{
    private const string QueueKey = "fg_offline_cmd_queue";

    public async Task EnqueueAsync(string method, string relativeUrl, object? body, CancellationToken ct = default)
    {
        var list = await ReadAllAsync(ct);
        var bodyJson = body == null ? null : JsonSerializer.Serialize(body, JsonOptions());
        list.Add(new QueuedCommand(method.ToUpperInvariant(), relativeUrl, bodyJson));
        await WriteAllAsync(list, ct);
    }

    public async Task<int> PendingCountAsync(CancellationToken ct = default) =>
        (await ReadAllAsync(ct)).Count;

    public async Task<(int sent, string? lastError)> TryFlushAsync(CancellationToken ct = default)
    {
        if (!await storage.IsOnlineAsync(ct))
            return (0, "Offline");

        var client = httpFactory.CreateClient("api");
        var pending = await ReadAllAsync(ct);
        var still = new List<QueuedCommand>();
        var sent = 0;
        string? err = null;

        foreach (var cmd in pending)
        {
            if (err != null)
            {
                still.Add(cmd);
                continue;
            }

            try
            {
                using var msg = new HttpRequestMessage(new HttpMethod(cmd.Method), cmd.Url);
                if (cmd.BodyJson != null)
                    msg.Content = new StringContent(cmd.BodyJson, Encoding.UTF8, "application/json");

                var res = await client.SendAsync(msg, ct);
                if (!res.IsSuccessStatusCode)
                {
                    var body = await res.Content.ReadAsStringAsync(ct);
                    err = $"{(int)res.StatusCode}: {body}";
                    still.Add(cmd);
                    continue;
                }

                sent++;
            }
            catch (Exception ex)
            {
                err = ApiConnectionErrors.Format(ex);
                still.Add(cmd);
            }
        }

        await WriteAllAsync(still, ct);
        return (sent, err);
    }

    private static JsonSerializerOptions JsonOptions() =>
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private async Task<List<QueuedCommand>> ReadAllAsync(CancellationToken ct)
    {
        var raw = await storage.GetAsync(QueueKey, ct);
        if (string.IsNullOrWhiteSpace(raw))
            return new List<QueuedCommand>();
        try
        {
            return JsonSerializer.Deserialize<List<QueuedCommand>>(raw, JsonOptions()) ?? new List<QueuedCommand>();
        }
        catch
        {
            return new List<QueuedCommand>();
        }
    }

    private async Task WriteAllAsync(List<QueuedCommand> list, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(list, JsonOptions());
        await storage.SetAsync(QueueKey, json, ct);
    }

    private sealed record QueuedCommand(string Method, string Url, string? BodyJson);
}
