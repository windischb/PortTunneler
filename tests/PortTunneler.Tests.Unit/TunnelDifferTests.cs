namespace PortTunneler.Tests.Unit;

public class TunnelDifferTests
{
    [Fact]
    public void Diff_AddedTunnel_DetectedAsAdded()
    {
        var oldServices = Array.Empty<TunnelConfig>();
        var newServices = new TunnelConfig[]
        {
            new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433 }
        };

        var result = TunnelDiffer.Diff(oldServices, newServices);

        Assert.Single(result.Added);
        Assert.Equal(1433, result.Added[0].ListenPort);
        Assert.Empty(result.RemovedPorts);
        Assert.Empty(result.Changed);
        Assert.Equal(0, result.UnchangedCount);
    }

    [Fact]
    public void Diff_RemovedTunnel_DetectedAsRemoved()
    {
        var oldServices = new TunnelConfig[]
        {
            new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433 }
        };
        var newServices = Array.Empty<TunnelConfig>();

        var result = TunnelDiffer.Diff(oldServices, newServices);

        Assert.Empty(result.Added);
        Assert.Single(result.RemovedPorts);
        Assert.Equal(1433, result.RemovedPorts[0]);
        Assert.Empty(result.Changed);
        Assert.Equal(0, result.UnchangedCount);
    }

    [Fact]
    public void Diff_ChangedTunnel_DetectedAsChanged()
    {
        var oldServices = new TunnelConfig[]
        {
            new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433 }
        };
        var newServices = new TunnelConfig[]
        {
            new DirectTunnelConfig { Name = "sql", ListenPort = 1433, TargetAddress = "host:1433" }
        };

        var result = TunnelDiffer.Diff(oldServices, newServices);

        Assert.Empty(result.Added);
        Assert.Empty(result.RemovedPorts);
        Assert.Single(result.Changed);
        Assert.Equal(1433, result.Changed[0].ListenPort);
        Assert.Equal(0, result.UnchangedCount);
    }

    [Fact]
    public void Diff_UnchangedTunnel_CountedAsUnchanged()
    {
        var oldServices = new TunnelConfig[]
        {
            new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433 }
        };
        var newServices = new TunnelConfig[]
        {
            new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433 }
        };

        var result = TunnelDiffer.Diff(oldServices, newServices);

        Assert.Empty(result.Added);
        Assert.Empty(result.RemovedPorts);
        Assert.Empty(result.Changed);
        Assert.Equal(1, result.UnchangedCount);
    }

    [Fact]
    public void Diff_MixedChanges_CorrectlyCategorized()
    {
        var oldServices = new TunnelConfig[]
        {
            new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433 },
            new DiscoverTunnelConfig { Name = "web", ListenPort = 8080 },
            new DirectTunnelConfig { Name = "redis", ListenPort = 6379, TargetAddress = "host:6379" }
        };
        var newServices = new TunnelConfig[]
        {
            new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433 },
            new DiscoverTunnelConfig { Name = "web-changed", ListenPort = 8080 },
            new DiscoverTunnelConfig { Name = "new-service", ListenPort = 9090 }
        };

        var result = TunnelDiffer.Diff(oldServices, newServices);

        Assert.Single(result.Added);
        Assert.Equal(9090, result.Added[0].ListenPort);
        Assert.Single(result.RemovedPorts);
        Assert.Equal(6379, result.RemovedPorts[0]);
        Assert.Single(result.Changed);
        Assert.Equal(8080, result.Changed[0].ListenPort);
        Assert.Equal(1, result.UnchangedCount);
    }

    [Fact]
    public void Diff_EmptyLists_ReturnsEmptyResult()
    {
        var result = TunnelDiffer.Diff([], []);

        Assert.Empty(result.Added);
        Assert.Empty(result.RemovedPorts);
        Assert.Empty(result.Changed);
        Assert.Equal(0, result.UnchangedCount);
    }

    [Fact]
    public void Diff_NameChange_OnSamePort_DetectedAsChanged()
    {
        var oldServices = new TunnelConfig[]
        {
            new DiscoverTunnelConfig { Name = "old-name", ListenPort = 1433 }
        };
        var newServices = new TunnelConfig[]
        {
            new DiscoverTunnelConfig { Name = "new-name", ListenPort = 1433 }
        };

        var result = TunnelDiffer.Diff(oldServices, newServices);

        Assert.Empty(result.Added);
        Assert.Empty(result.RemovedPorts);
        Assert.Single(result.Changed);
        Assert.Equal(0, result.UnchangedCount);
    }

    [Fact]
    public void Diff_MetadataOnlyRename_TreatedAsUnchanged()
    {
        var oldServices = new TunnelConfig[]
        {
            new DiscoverTunnelConfig { Name = "mssql", ListenPort = 1433 }
        };
        var newServices = new TunnelConfig[]
        {
            new DiscoverTunnelConfig { Name = "MSSQL Prod", ListenPort = 1433, ServiceTag = "mssql" }
        };

        var result = TunnelDiffer.Diff(oldServices, newServices);

        Assert.Empty(result.Added);
        Assert.Empty(result.RemovedPorts);
        Assert.Empty(result.Changed);
        Assert.Equal(1, result.UnchangedCount);
    }

    [Fact]
    public void Diff_ServerAddressChange_DetectedAsChanged()
    {
        var oldServices = new TunnelConfig[]
        {
            new TunnelTunnelConfig { Name = "sql", ListenPort = 1433, ServerAddress = "host1:51000" }
        };
        var newServices = new TunnelConfig[]
        {
            new TunnelTunnelConfig { Name = "sql", ListenPort = 1433, ServerAddress = "host2:51000" }
        };

        var result = TunnelDiffer.Diff(oldServices, newServices);

        Assert.Single(result.Changed);
        Assert.Equal(0, result.UnchangedCount);
    }
}
