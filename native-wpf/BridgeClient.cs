using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace FastDownloader.Native;

public sealed class TransferItem
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Status { get; init; } = "";
    public string StatusLabel { get; init; } = "";
    public string Detail { get; init; } = "";
    public double Progress { get; init; }
    public string ProgressLabel { get; init; } = "";
    public string ActionLabel { get; init; } = "";
    public Visibility ActionVisibility { get; init; }
    public long Speed { get; init; }
    public bool IsActive => Status is "active" or "downloading" or "processing";
    public bool IsCompleted => Status is "complete" or "completed";
    public bool IsQueued => Status is "waiting" or "paused" or "queued";
}

public sealed class BridgeSnapshot
{
    public bool Connected { get; init; }
    public string DownloadFolder { get; init; } = "";
    public string EngineStatus { get; init; } = "";
    public List<TransferItem> Transfers { get; init; } = new();
}

public sealed class BridgeClient : IDisposable
{
    private readonly HttpClient http = new() {
        BaseAddress = new Uri("http://127.0.0.1:6801/"),
        Timeout = TimeSpan.FromSeconds(3)
    };

    private static string Field(JsonElement parent, string name, string fallback = "")
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return fallback;
        return value.ToString();
    }

    private static long Number(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var element)) return 0;
        return long.TryParse(element.ToString(), out var number) ? number : 0;
    }

    private static double Decimal(JsonElement parent, string name, double fallback = 0)
    {
        if (!parent.TryGetProperty(name, out var element)) return fallback;
        return double.TryParse(element.ToString(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    public static string Bytes(long size)
    {
        if (size < 1024) return size + " B";
        string[] units = { "KB", "MB", "GB", "TB" };
        double value = size;
        for (var i = 0; i < units.Length; i++)
        {
            value /= 1024;
            if (value < 1024 || i == units.Length - 1)
                return value.ToString(value < 10 ? "0.0" : "0", System.Globalization.CultureInfo.InvariantCulture) + " " + units[i];
        }
        return size + " B";
    }

    public async Task<BridgeSnapshot> SnapshotAsync()
    {
        using var response = await http.GetAsync("api/status");
        response.EnsureSuccessStatusCode();
        using var statusDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var status = statusDocument.RootElement;
        if (Field(status, "status") != "running") throw new InvalidOperationException("The local download engine is not ready.");
        using var tasksResponse = await http.GetAsync("api/tasks");
        tasksResponse.EnsureSuccessStatusCode();
        using var tasksDocument = JsonDocument.Parse(await tasksResponse.Content.ReadAsStringAsync());
        var transfers = new List<TransferItem>();
        if (tasksDocument.RootElement.TryGetProperty("tasks", out var tasks) && tasks.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in tasks.EnumerateArray())
            {
                string id = Field(item, "gid");
                string state = Field(item, "status", "unknown").ToLowerInvariant();
                string name = Field(item, "name", "Unnamed transfer");
                long downloaded = Number(item, "completedLength");
                long total = Number(item, "totalLength");
                long speed = Number(item, "downloadSpeed");
                var progress = Math.Clamp(Decimal(item, "progressExact", -1), 0, 100);
                if (total > 0 && progress <= 0) progress = Math.Clamp(100.0 * downloaded / total, 0, 100);
                string source = Field(item, "engine");
                string statusLabel = state switch {
                    "active" or "downloading" => "Downloading",
                    "processing" => "Processing",
                    "complete" or "completed" => "Complete",
                    "paused" => "Paused",
                    "waiting" or "queued" => "Queued",
                    "error" => "Failed",
                    _ => state
                };
                string action = state is "active" or "downloading" or "processing" ? "Pause" : "Resume";
                bool canAct = state is "active" or "downloading" or "processing" or "paused" or "waiting" or "error";
                string progressText = total == 0 && state is not ("complete" or "completed")
                    ? "—" : progress.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
                string detail = total > 0 ? Bytes(downloaded) + " / " + Bytes(total) : Bytes(downloaded);
                detail += "  ·  " + (string.IsNullOrWhiteSpace(source) ? "transfer" : source);
                transfers.Add(new TransferItem {
                    Id = id, Name = name, Status = state, StatusLabel = statusLabel,
                    Detail = detail, Speed = speed, Progress = progress, ProgressLabel = progressText,
                    ActionLabel = action, ActionVisibility = canAct && !string.IsNullOrWhiteSpace(id)
                        ? Visibility.Visible : Visibility.Collapsed
                });
            }
        }
        return new BridgeSnapshot {
            Connected = true, DownloadFolder = Field(status, "downloads"),
            EngineStatus = Field(status, "engine"), Transfers = transfers
        };
    }

    private async Task<JsonDocument> PostAsync(string route, object payload)
    {
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(route, content);
        string json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("The download engine rejected the request (" + (int)response.StatusCode + ").");
        return JsonDocument.Parse(json);
    }

    public async Task ActionAsync(string action, string gid = "")
    {
        using var result = await PostAsync("api/action", new { action, gid });
        if (Field(result.RootElement, "ok") != "True" && Field(result.RootElement, "ok") != "true")
            throw new InvalidOperationException("The download action was not accepted.");
    }

    public async Task AddAsync(string url, string filename, bool audio, bool startNow)
    {
        using var result = await PostAsync("api/download", new {
            url,
            filename,
            title = filename,
            type = audio ? "video" : "auto",
            category = audio ? "Music" : "General",
            quality = audio ? "audio" : "best",
            direct = true,
            startNow
        });
        if (Field(result.RootElement, "ok") != "True" && Field(result.RootElement, "ok") != "true")
            throw new InvalidOperationException("The engine did not accept the download.");
    }

    public void Dispose() => http.Dispose();
}