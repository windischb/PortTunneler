using System.IO.Pipes;
using System.Text;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace PortTunneler.Tests.Unit;

public class CommandPipeServiceTests
{
    [Fact]
    public void GetPipeName_ReturnsConsistentName()
    {
        var name1 = CommandPipeService.GetPipeName();
        var name2 = CommandPipeService.GetPipeName();

        Assert.Equal(name1, name2);
        Assert.StartsWith("porttunneler-", name1);
        Assert.Equal(13 + 12, name1.Length); // "porttunneler-" (13) + 12 hex chars
    }

    [Fact]
    public async Task SendReload_WithValidConfig_ReturnsSuccess()
    {
        var config = new PortTunnelerConfig();
        var logger = Substitute.For<ILogger<CommandPipeService>>();
        var service = new CommandPipeService(config, logger);

        using var cts = new CancellationTokenSource();
        var executeTask = StartServiceAsync(service, cts.Token);

        try
        {
            var pipeName = CommandPipeService.GetPipeName();
            await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(connectCts.Token);

            await using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);

            await writer.WriteLineAsync("reload".AsMemory(), connectCts.Token);
            var response = await reader.ReadLineAsync(connectCts.Token);

            Assert.NotNull(response);
            Assert.StartsWith("Reload complete:", response);
        }
        finally
        {
            await cts.CancelAsync();
            await WaitForServiceStop(executeTask);
        }
    }

    [Fact]
    public async Task SendUnknownCommand_ReturnsUnknown()
    {
        var config = new PortTunnelerConfig();
        var logger = Substitute.For<ILogger<CommandPipeService>>();
        var service = new CommandPipeService(config, logger);

        using var cts = new CancellationTokenSource();
        var executeTask = StartServiceAsync(service, cts.Token);

        try
        {
            var pipeName = CommandPipeService.GetPipeName();
            await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(connectCts.Token);

            await using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);

            await writer.WriteLineAsync("unknown-cmd".AsMemory(), connectCts.Token);
            var response = await reader.ReadLineAsync(connectCts.Token);

            Assert.NotNull(response);
            Assert.Contains("Unknown command", response);
        }
        finally
        {
            await cts.CancelAsync();
            await WaitForServiceStop(executeTask);
        }
    }

    private static Task StartServiceAsync(CommandPipeService service, CancellationToken ct)
    {
        // Use reflection to call protected ExecuteAsync
        var method = typeof(CommandPipeService).GetMethod(
            "ExecuteAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return (Task)method!.Invoke(service, [ct])!;
    }

    private static async Task WaitForServiceStop(Task executeTask)
    {
        // On Windows, WaitForConnectionAsync may not respect cancellation.
        // Connect a dummy client to unblock the pipe server loop.
        try
        {
            await using var unblock = new NamedPipeClientStream(".", CommandPipeService.GetPipeName(), PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await unblock.ConnectAsync(timeout.Token);
        }
        catch
        {
            // Pipe may already be closed
        }

        try
        {
            await executeTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (TimeoutException)
        {
            // Acceptable timeout
        }
    }
}
