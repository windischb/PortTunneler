using System.Buffers.Binary;
using System.Text;

namespace PortTunneler.Tests.Unit;

public class TunnelProtocolTests
{
    [Fact]
    public async Task WriteTag_ReadTag_RoundTrip()
    {
        var stream = new MemoryStream();

        await TunnelProtocol.WriteTagAsync(stream, "hello", CancellationToken.None);

        stream.Position = 0;
        var result = await TunnelProtocol.ReadTagAsync(stream, CancellationToken.None);

        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task ReadTag_EmptyStream_ReturnsNull()
    {
        var stream = new MemoryStream();

        var result = await TunnelProtocol.ReadTagAsync(stream, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReadTag_PartialLengthHeader_ReturnsNull()
    {
        var stream = new MemoryStream([0x01, 0x00]); // Only 2 bytes of 4-byte header

        var result = await TunnelProtocol.ReadTagAsync(stream, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReadTag_NegativeLength_ThrowsInvalidDataException()
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, -1);
        var stream = new MemoryStream(buffer);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            TunnelProtocol.ReadTagAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task ReadTag_OversizedLength_ThrowsInvalidDataException()
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 70000);
        var stream = new MemoryStream(buffer);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            TunnelProtocol.ReadTagAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task WriteTag_PingTag_RoundTrips()
    {
        var stream = new MemoryStream();

        await TunnelProtocol.WriteTagAsync(stream, "ping", CancellationToken.None);

        stream.Position = 0;
        var result = await TunnelProtocol.ReadTagAsync(stream, CancellationToken.None);

        Assert.Equal("ping", result);
    }

    [Fact]
    public async Task WritePong_AsTag_RoundTrips()
    {
        var stream = new MemoryStream();

        await TunnelProtocol.WriteTagAsync(stream, "pong", CancellationToken.None);

        stream.Position = 0;
        var result = await TunnelProtocol.ReadTagAsync(stream, CancellationToken.None);

        Assert.Equal("pong", result);
    }

    [Fact]
    public async Task WriteTag_UnicodeContent_RoundTrips()
    {
        var stream = new MemoryStream();

        await TunnelProtocol.WriteTagAsync(stream, "\u670D\u52A1\u53D1\u73B0", CancellationToken.None);

        stream.Position = 0;
        var result = await TunnelProtocol.ReadTagAsync(stream, CancellationToken.None);

        Assert.Equal("\u670D\u52A1\u53D1\u73B0", result);
    }

    [Fact]
    public async Task WriteTag_WritesCorrectFormat()
    {
        var stream = new MemoryStream();
        var tag = "test";
        var expectedTagBytes = Encoding.UTF8.GetBytes(tag);

        await TunnelProtocol.WriteTagAsync(stream, tag, CancellationToken.None);

        var data = stream.ToArray();
        Assert.Equal(4 + expectedTagBytes.Length, data.Length);
        Assert.Equal(expectedTagBytes.Length, BinaryPrimitives.ReadInt32LittleEndian(data));
        Assert.Equal(tag, Encoding.UTF8.GetString(data, 4, expectedTagBytes.Length));
    }

    [Fact]
    public async Task ReadTag_StreamClosedAfterHeader_ReturnsNull()
    {
        // Valid 4-byte header saying 5 bytes follow, but only 2 tag bytes present
        var stream = new MemoryStream([0x05, 0x00, 0x00, 0x00, 0x68, 0x69]);

        var result = await TunnelProtocol.ReadTagAsync(stream, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReadTag_CancellationRequested_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var stream = new MemoryStream([0x04, 0x00, 0x00, 0x00, 0x74, 0x65, 0x73, 0x74]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            TunnelProtocol.ReadTagAsync(stream, cts.Token));
    }
}
