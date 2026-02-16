using System.Buffers.Binary;
using System.Text;

namespace PortTunneler;

public static class TunnelProtocol
{
    public static async Task<string?> ReadTagAsync(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        try
        {
            await stream.ReadExactlyAsync(lengthBuffer, cancellationToken);
        }
        catch (EndOfStreamException)
        {
            return null;
        }

        var tagLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);

        if (tagLength <= 0 || tagLength > 65535)
            throw new InvalidDataException($"Invalid tag length: {tagLength}");

        var tagBuffer = new byte[tagLength];
        await stream.ReadExactlyAsync(tagBuffer, cancellationToken);

        return Encoding.UTF8.GetString(tagBuffer);
    }

    public static async Task WriteTagAsync(Stream stream, string tag, CancellationToken cancellationToken)
    {
        var tagBytes = Encoding.UTF8.GetBytes(tag);
        var lengthBuffer = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer, tagBytes.Length);

        await stream.WriteAsync(lengthBuffer, cancellationToken);
        await stream.WriteAsync(tagBytes, cancellationToken);
    }

    public static async Task WritePongAsync(Stream stream, CancellationToken cancellationToken)
    {
        var pongBuffer = "pong"u8.ToArray();
        await stream.WriteAsync(pongBuffer, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<string?> ReadPongAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[4];
        try
        {
            await stream.ReadExactlyAsync(buffer, cancellationToken);
        }
        catch (EndOfStreamException)
        {
            return null;
        }

        return Encoding.UTF8.GetString(buffer);
    }
}
