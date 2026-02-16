using System.Net;
using System.Net.Sockets;

namespace PortTunneler.Tests.Integration;

public static class TestHelpers
{
    public static int GetAvailablePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    public static async Task<TcpListener> StartEchoServer(int port, CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync(ct);
                    _ = HandleEchoClient(client, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }, ct);

        return listener;
    }

    private static async Task HandleEchoClient(TcpClient client, CancellationToken ct)
    {
        await using var stream = client.GetStream();
        var buffer = new byte[8192];
        try
        {
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            }
        }
        catch
        {
            // Expected on client disconnect
        }
        finally
        {
            client.Dispose();
        }
    }

    public static async Task<TcpClient> ConnectWithRetryAsync(string host, int port, int retries = 10, int delayMs = 100)
    {
        for (var i = 0; i < retries; i++)
        {
            try
            {
                var client = new TcpClient();
                await client.ConnectAsync(host, port);
                return client;
            }
            catch (SocketException) when (i < retries - 1)
            {
                await Task.Delay(delayMs);
            }
        }

        throw new Exception($"Could not connect to {host}:{port} after {retries} retries.");
    }
}
