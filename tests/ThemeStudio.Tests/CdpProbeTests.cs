using System.Net;
using System.Net.Sockets;
using System.Text;
using ThemeStudio.Adapter;
using Xunit;

namespace ThemeStudio.Tests;

public sealed class CdpProbeTests
{
    [Fact]
    public async Task CheckAsync_AcceptsLoopbackEndpointAndReturnsBrowserIdentity()
    {
        await using var server = await MockCdpServer.StartAsync(port =>
        [
            ("/json/version", VersionJson($"ws://127.0.0.1:{port}/devtools/browser/test")),
            ("/json/list", "[]")
        ]);
        using var probe = new CdpProbe();

        var result = await probe.CheckAsync(server.Port);

        Assert.True(result.Responded);
        Assert.Equal("Chrome/155.0.8059.40", result.Browser);
        Assert.Equal(0, result.PageTargetCount);
        Assert.Contains("no renderer page targets", result.Message);
    }

    [Fact]
    public async Task CheckAsyncCountsPageTargetsWithoutReadingTheirUrls()
    {
        await using var server = await MockCdpServer.StartAsync(port =>
        [
            ("/json/version", VersionJson($"ws://127.0.0.1:{port}/devtools/browser/test")),
            ("/json/list", "[{\"type\":\"page\",\"url\":\"https://chatgpt.com/c/private-thread\"},{\"type\":\"service_worker\"}]")
        ]);
        using var probe = new CdpProbe();

        var result = await probe.CheckAsync(server.Port);

        Assert.True(result.Responded);
        Assert.Equal(1, result.PageTargetCount);
        Assert.DoesNotContain("private-thread", result.Message);
        Assert.Contains("identity and theme compatibility remain unverified", result.Message);
    }

    [Theory]
    [InlineData("ws://192.0.2.10:{0}/devtools/browser/test")]
    [InlineData("wss://127.0.0.1:{0}/devtools/browser/test")]
    [InlineData("ws://127.0.0.1:{1}/devtools/browser/test")]
    public async Task CheckAsync_RejectsNonLoopbackOrMismatchedDebuggerSocket(string socketTemplate)
    {
        await using var server = await MockCdpServer.StartAsync(port =>
        [
            ("/json/version", VersionJson(socketTemplate.Replace("{0}", port.ToString()).Replace("{1}", (port + 1).ToString())))
        ]);
        using var probe = new CdpProbe();

        var result = await probe.CheckAsync(server.Port);

        Assert.False(result.Responded);
        Assert.Contains("rejected", result.Message);
    }

    [Fact]
    public async Task CheckAsync_RejectsInvalidPortsWithoutMakingARequest()
    {
        using var probe = new CdpProbe();

        var result = await probe.CheckAsync(0);

        Assert.False(result.Responded);
        Assert.Contains("Invalid", result.Message);
    }

    private static string VersionJson(string debuggerUrl) =>
        $$"""{"Browser":"Chrome/155.0.8059.40","webSocketDebuggerUrl":"{{debuggerUrl}}"}""";

    private sealed class MockCdpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _requestTask;

        private MockCdpServer(TcpListener listener, int port, Task requestTask)
        {
            _listener = listener;
            Port = port;
            _requestTask = requestTask;
        }

        public int Port { get; }

        public static Task<MockCdpServer> StartAsync(Func<int, (string Path, string Body)[]> responses)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var responseList = responses(port).ToDictionary(item => item.Path, item => Encoding.UTF8.GetBytes(item.Body));
            var requestTask = Task.Run(async () =>
            {
                foreach (var (path, body) in responseList)
                {
                    using var client = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    await using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                    var requestLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    while (await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)) is { Length: > 0 }) { }
                    var status = requestLine?.Split(' ').ElementAtOrDefault(1) == path ? "200 OK" : "404 Not Found";
                    var responseBody = status.StartsWith("200", StringComparison.Ordinal) ? body : "[]"u8.ToArray();
                    var header = Encoding.ASCII.GetBytes(
                        $"HTTP/1.1 {status}\r\nContent-Type: application/json\r\nContent-Length: {responseBody.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(header);
                    await stream.WriteAsync(responseBody);
                }
            });

            return Task.FromResult(new MockCdpServer(listener, port, requestTask));
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await _requestTask;
        }
    }
}
