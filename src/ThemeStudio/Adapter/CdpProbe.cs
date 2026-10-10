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
        if (port is < 1 or > 65535) return new(false, null, null, "Invalid local debugging port.");
        var uri = new Uri($"http://127.0.0.1:{port}/json/version");
        try
        {
            using var response = await _http.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, null, null, $"Local CDP returned HTTP {(int)response.StatusCode}.");
            var version = await response.Content.ReadFromJsonAsync<CdpVersion>(_json, cancellationToken);
            if (version is null || string.IsNullOrWhiteSpace(version.Browser)) return new(false, null, null, "The local endpoint did not return a valid browser identity.");
            if (!Uri.TryCreate(version.WebSocketDebuggerUrl, UriKind.Absolute, out var ws) ||
                ws.Scheme != "ws" || ws.Port != port ||
                !(string.Equals(ws.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                  (IPAddress.TryParse(ws.Host, out var address) && IPAddress.IsLoopback(address))))
                return new(false, version.Browser, null, "The debugger WebSocket was not a loopback address; it was rejected.");

            using var targetsResponse = await _http.GetAsync(new UriBuilder(uri) { Path = "/json/list" }.Uri, cancellationToken);
            if (!targetsResponse.IsSuccessStatusCode)
                return new(true, version.Browser, null, "The loopback endpoint answered, but renderer targets could not be listed. The client and theme support remain unverified.");

            var targets = await targetsResponse.Content.ReadFromJsonAsync<CdpTarget[]>(_json, cancellationToken);
            if (targets is null)
                return new(true, version.Browser, null, "The loopback endpoint returned an invalid renderer-target list. The client and theme support remain unverified.");

            var pageTargets = targets.Count(target => string.Equals(target.Type, "page", StringComparison.OrdinalIgnoreCase));
            var message = pageTargets == 0
                ? "The loopback endpoint answered, but exposed no renderer page targets. Theme application remains unavailable."
                : $"The loopback endpoint exposed {pageTargets} renderer page target(s). Client identity and theme compatibility remain unverified.";
            return new(true, version.Browser, pageTargets, message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, null, null, "No local CDP endpoint responded. The live adapter remains unavailable.");
        }
        catch (HttpRequestException)
        {
            return new(false, null, null, "No local CDP endpoint responded. The live adapter remains unavailable.");
        }
        catch (System.Text.Json.JsonException)
        {
            return new(false, null, null, "The local endpoint returned invalid debugging metadata.");
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

    private sealed class CdpTarget
    {
        public string Type { get; set; } = "";
    }
}

public sealed record CdpProbeResult(bool Responded, string? Browser, int? PageTargetCount, string Message);
