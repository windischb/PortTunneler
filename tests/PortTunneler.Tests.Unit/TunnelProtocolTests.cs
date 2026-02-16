using System.Buffers.Binary;
using System.Text;
using FluentAssertions;

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

        result.Should().Be("hello");
    }

    [Fact]
    public async Task ReadTag_EmptyStream_ReturnsNull()
    {
        var stream = new MemoryStream();

        var result = await TunnelProtocol.ReadTagAsync(stream, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ReadTag_PartialLengthHeader_ReturnsNull()
    {
        var stream = new MemoryStream([0x01, 0x00]); // Only 2 bytes of 4-byte header

        var result = await TunnelProtocol.ReadTagAsync(stream, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ReadTag_NegativeLength_ThrowsInvalidDataException()
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, -1);
        var stream = new MemoryStream(buffer);

        var act = () => TunnelProtocol.ReadTagAsync(stream, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task ReadTag_OversizedLength_ThrowsInvalidDataException()
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 70000);
        var stream = new MemoryStream(buffer);

        var act = () => TunnelProtocol.ReadTagAsync(stream, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task WriteTag_PingTag_RoundTrips()
    {
        var stream = new MemoryStream();

        await TunnelProtocol.WriteTagAsync(stream, "ping", CancellationToken.None);

        stream.Position = 0;
        var result = await TunnelProtocol.ReadTagAsync(stream, CancellationToken.None);

        result.Should().Be("ping");
    }

    [Fact]
    public async Task WritePong_ReadPong_RoundTrip()
    {
        var stream = new MemoryStream();

        await TunnelProtocol.WritePongAsync(stream, CancellationToken.None);

        stream.Position = 0;
        var result = await TunnelProtocol.ReadPongAsync(stream, CancellationToken.None);

        result.Should().Be("pong");
    }

    [Fact]
    public async Task ReadPong_EmptyStream_ReturnsNull()
    {
        var stream = new MemoryStream();

        var result = await TunnelProtocol.ReadPongAsync(stream, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task WriteTag_UnicodeContent_RoundTrips()
    {
        var stream = new MemoryStream();

        await TunnelProtocol.WriteTagAsync(stream, "服务发现", CancellationToken.None);

        stream.Position = 0;
        var result = await TunnelProtocol.ReadTagAsync(stream, CancellationToken.None);

        result.Should().Be("服务发现");
    }

    [Fact]
    public async Task WriteTag_WritesCorrectFormat()
    {
        var stream = new MemoryStream();
        var tag = "test";
        var expectedTagBytes = Encoding.UTF8.GetBytes(tag);

        await TunnelProtocol.WriteTagAsync(stream, tag, CancellationToken.None);

        var data = stream.ToArray();
        data.Length.Should().Be(4 + expectedTagBytes.Length);
        BinaryPrimitives.ReadInt32LittleEndian(data).Should().Be(expectedTagBytes.Length);
        Encoding.UTF8.GetString(data, 4, expectedTagBytes.Length).Should().Be(tag);
    }

    [Fact]
    public async Task ReadTag_CancellationRequested_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var stream = new MemoryStream([0x04, 0x00, 0x00, 0x00, 0x74, 0x65, 0x73, 0x74]);

        var act = () => TunnelProtocol.ReadTagAsync(stream, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
