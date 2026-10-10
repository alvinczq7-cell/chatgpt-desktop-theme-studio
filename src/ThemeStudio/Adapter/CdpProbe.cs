using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ThemeStudio.Adapter;

public sealed class CdpProbe : IDisposable
{
    private readonly HttpClient _http = new(new SocketsHttpHandler
    {
        UseProxy = false,
        AllowAutoRedirect = false,
        ConnectTimeout = TimeSpan.FromSeconds(2)
    }) { Timeout = TimeSpan.FromSeconds(3) };

    public async Task<CdpProbeResult> CheckAsync(int port = 9335, CancellationToken cancellationToken = default)
    {
        if (port is < 1 or > 65535) return new(false, null, "Invalid local debugging port.");
        var uri = new Uri($"http://127.0.0.1:{port}/json/version");
        try
        {
            using var response = await _http.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, null, $"Local CDP returned HTTP {(int)response.StatusCode}.");
            var version = await response.Content.ReadFromJsonAsync<CdpVersion>(_json, cancellationToken);
            if (version is null || string.IsNullOrWhiteSpace(version.Browser)) return new(false, null, "The local endpoint did not return a valid browser identity.");
            if (!Uri.TryCreate(version.WebSocketDebuggerUrl, UriKind.Absolute, out var ws) ||
                ws.Scheme != "ws" || ws.Port != port ||
                !(string.Equals(ws.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                  (IPAddress.TryParse(ws.Host, out var address) && IPAddress.IsLoopback(address))))
                return new(false, version.Browser, "The debugger WebSocket was not a loopback address; it was rejected.");
            return new(true, version.Browser, "A loopback CDP endpoint was found. This does not verify the ChatGPT client or enable theme injection.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, null, "No local CDP endpoint responded. The live adapter remains unavailable.");
        }
        catch (HttpRequestException)
        {
            return new(false, null, "No local CDP endpoint responded. The live adapter remains unavailable.");
        }
        catch (System.Text.Json.JsonException)
        {
            return new(false, null, "The local endpoint returned invalid debugging metadata.");
        }
    }

    public void Dispose() => _http.Dispose();

    private static readonly System.Text.Json.JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class CdpVersion
    {
        public string Browser { get; set; } = "";
        public string WebSocketDebuggerUrl { get; set; } = "";
    }
}

public sealed record CdpProbeResult(bool Responded, string? Browser, string Message);
