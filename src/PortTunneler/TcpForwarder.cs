using System.Buffers;
using Microsoft.Extensions.Logging;

namespace PortTunneler;

public static class TcpForwarder
{
    private const int BufferSize = 8192;

    public static async Task ForwardAsync(Stream input, Stream output, ILogger logger, string direction, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer.AsMemory(0, BufferSize), ct)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                logger.LogDebug("{Direction}: Forwarded {BytesRead} bytes.", direction, bytesRead);
            }
        }
        catch (IOException ioEx)
        {
            logger.LogDebug(ioEx, "{Direction}: IO Exception (connection closed).", direction);
        }
        catch (ObjectDisposedException)
        {
            logger.LogDebug("{Direction}: Connection closed.", direction);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("{Direction}: Operation canceled.", direction);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in {Direction}.", direction);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
