namespace PortTunneler;

public static class TunnelDiffer
{
    public sealed record DiffResult(
        IReadOnlyList<TunnelConfig> Added,
        IReadOnlyList<int> RemovedPorts,
        IReadOnlyList<TunnelConfig> Changed,
        int UnchangedCount);

    public static DiffResult Diff(
        IReadOnlyList<TunnelConfig> oldServices,
        IReadOnlyList<TunnelConfig> newServices)
    {
        var oldByPort = new Dictionary<int, TunnelConfig>();
        foreach (var svc in oldServices)
            oldByPort[svc.ListenPort] = svc;

        var newByPort = new Dictionary<int, TunnelConfig>();
        foreach (var svc in newServices)
            newByPort[svc.ListenPort] = svc;

        var added = new List<TunnelConfig>();
        var removedPorts = new List<int>();
        var changed = new List<TunnelConfig>();
        var unchangedCount = 0;

        foreach (var (port, newConfig) in newByPort)
        {
            if (!oldByPort.TryGetValue(port, out var oldConfig))
            {
                added.Add(newConfig);
            }
            else if (!TunnelConfigComparer.Equals(oldConfig, newConfig))
            {
                changed.Add(newConfig);
            }
            else
            {
                unchangedCount++;
            }
        }

        foreach (var port in oldByPort.Keys)
        {
            if (!newByPort.ContainsKey(port))
            {
                removedPorts.Add(port);
            }
        }

        return new DiffResult(added, removedPorts, changed, unchangedCount);
    }

    public sealed record ServerDiffResult(
        IReadOnlyList<string> RemovedWireTags,
        IReadOnlyList<string> ChangedWireTags);

    public static ServerDiffResult DiffServer(
        IReadOnlyList<ExposedServiceConfig> oldServices,
        IReadOnlyList<ExposedServiceConfig> newServices)
    {
        var oldByTag = new Dictionary<string, ExposedServiceConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var svc in oldServices)
            oldByTag[svc.WireTag] = svc;

        var newByTag = new Dictionary<string, ExposedServiceConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var svc in newServices)
            newByTag[svc.WireTag] = svc;

        var removedWireTags = new List<string>();
        var changedWireTags = new List<string>();

        foreach (var (tag, oldConfig) in oldByTag)
        {
            if (!newByTag.TryGetValue(tag, out var newConfig))
            {
                removedWireTags.Add(tag);
            }
            else if (!TunnelConfigComparer.ServiceEquals(oldConfig, newConfig))
            {
                changedWireTags.Add(tag);
            }
        }

        return new ServerDiffResult(removedWireTags, changedWireTags);
    }
}
