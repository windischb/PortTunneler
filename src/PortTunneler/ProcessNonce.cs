namespace PortTunneler;

public sealed class ProcessNonce
{
    public string Value { get; } = Guid.NewGuid().ToString("N");
}
