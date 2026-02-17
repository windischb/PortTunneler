namespace PortTunneler;

public static class TunnelConfigComparer
{
    public static bool Equals(TunnelConfig a, TunnelConfig b)
    {
        if (a.GetType() != b.GetType())
            return false;

        if (a.ListenPort != b.ListenPort || a.WireTag != b.WireTag)
            return false;

        return (a, b) switch
        {
            (TunnelTunnelConfig ta, TunnelTunnelConfig tb) => ta.ServerAddress == tb.ServerAddress,
            (DirectTunnelConfig da, DirectTunnelConfig db) => da.TargetAddress == db.TargetAddress,
            (DiscoverTunnelConfig, DiscoverTunnelConfig) => true,
            _ => false
        };
    }

    public static bool ServiceEquals(ExposedServiceConfig a, ExposedServiceConfig b)
    {
        return a.WireTag == b.WireTag
               && a.TargetAddress == b.TargetAddress;
    }

    public static bool ServiceListEquals(IReadOnlyList<ExposedServiceConfig> a, IReadOnlyList<ExposedServiceConfig> b)
    {
        if (a.Count != b.Count)
            return false;

        for (var i = 0; i < a.Count; i++)
        {
            if (!ServiceEquals(a[i], b[i]))
                return false;
        }

        return true;
    }
}
