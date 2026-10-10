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
        await using var server = await MockCdpServer.StartAsync(port => VersionJson(
            $"ws://127.0.0.1:{port}/devtools/browser/test"));
        using var probe = new CdpProbe();

        var result = await probe.CheckAsync(server.Port);

        Assert.True(result.Responded);
        Assert.Equal("Chrome/155.0.8059.40", result.Browser);
        Assert.Contains("does not verify the ChatGPT client", result.Message);
    }

    [Theory]
    [InlineData("ws://192.0.2.10:{0}/devtools/browser/test")]
    [InlineData("wss://127.0.0.1:{0}/devtools/browser/test")]
    [InlineData("ws://127.0.0.1:{1}/devtools/browser/test")]
    public async Task CheckAsync_RejectsNonLoopbackOrMismatchedDebuggerSocket(string socketTemplate)
    {
        await using var server = await MockCdpServer.StartAsync(port => VersionJson(
            socketTemplate.Replace("{0}", port.ToString()).Replace("{1}", (port + 1).ToString())));
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

        public static Task<MockCdpServer> StartAsync(Func<int, string> responseBody)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var body = Encoding.UTF8.GetBytes(responseBody(port));
            var requestTask = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                while (await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)) is { Length: > 0 }) { }

                var header = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header);
                await stream.WriteAsync(body);
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
