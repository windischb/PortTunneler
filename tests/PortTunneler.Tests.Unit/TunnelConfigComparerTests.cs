namespace PortTunneler.Tests.Unit;

public class TunnelConfigComparerTests
{
    [Fact]
    public void Equals_IdenticalDiscoverConfigs_ReturnsTrue()
    {
        var a = new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433, ServiceTag = "tag" };
        var b = new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433, ServiceTag = "tag" };

        Assert.True(TunnelConfigComparer.Equals(a, b));
    }

    [Fact]
    public void Equals_IdenticalTunnelConfigs_ReturnsTrue()
    {
        var a = new TunnelTunnelConfig { Name = "sql", ListenPort = 1433, ServerAddress = "host:51000" };
        var b = new TunnelTunnelConfig { Name = "sql", ListenPort = 1433, ServerAddress = "host:51000" };

        Assert.True(TunnelConfigComparer.Equals(a, b));
    }

    [Fact]
    public void Equals_IdenticalDirectConfigs_ReturnsTrue()
    {
        var a = new DirectTunnelConfig { Name = "sql", ListenPort = 1433, TargetAddress = "host:1433" };
        var b = new DirectTunnelConfig { Name = "sql", ListenPort = 1433, TargetAddress = "host:1433" };

        Assert.True(TunnelConfigComparer.Equals(a, b));
    }

    [Fact]
    public void Equals_DifferentName_ReturnsFalse()
    {
        var a = new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433 };
        var b = new DiscoverTunnelConfig { Name = "web", ListenPort = 1433 };

        Assert.False(TunnelConfigComparer.Equals(a, b));
    }

    [Fact]
    public void Equals_DifferentPort_ReturnsFalse()
    {
        var a = new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433 };
        var b = new DiscoverTunnelConfig { Name = "sql", ListenPort = 1434 };

        Assert.False(TunnelConfigComparer.Equals(a, b));
    }

    [Fact]
    public void Equals_DifferentWireTag_ReturnsFalse()
    {
        var a = new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433, ServiceTag = "a" };
        var b = new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433, ServiceTag = "b" };

        Assert.False(TunnelConfigComparer.Equals(a, b));
    }

    [Fact]
    public void Equals_DifferentNameButSameWireTag_ReturnsTrue()
    {
        var a = new DiscoverTunnelConfig { Name = "mssql", ListenPort = 1433 };
        var b = new DiscoverTunnelConfig { Name = "MSSQL Prod", ListenPort = 1433, ServiceTag = "mssql" };

        Assert.True(TunnelConfigComparer.Equals(a, b));
    }

    [Fact]
    public void Equals_DifferentType_ReturnsFalse()
    {
        var a = new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433 };
        var b = new DirectTunnelConfig { Name = "sql", ListenPort = 1433, TargetAddress = "host:1433" };

        Assert.False(TunnelConfigComparer.Equals(a, b));
    }

    [Fact]
    public void Equals_DifferentServerAddress_ReturnsFalse()
    {
        var a = new TunnelTunnelConfig { Name = "sql", ListenPort = 1433, ServerAddress = "host1:51000" };
        var b = new TunnelTunnelConfig { Name = "sql", ListenPort = 1433, ServerAddress = "host2:51000" };

        Assert.False(TunnelConfigComparer.Equals(a, b));
    }

    [Fact]
    public void Equals_DifferentTargetAddress_ReturnsFalse()
    {
        var a = new DirectTunnelConfig { Name = "sql", ListenPort = 1433, TargetAddress = "host1:1433" };
        var b = new DirectTunnelConfig { Name = "sql", ListenPort = 1433, TargetAddress = "host2:1433" };

        Assert.False(TunnelConfigComparer.Equals(a, b));
    }

    [Fact]
    public void Equals_NullVsNonNullServiceTag_ReturnsFalse()
    {
        var a = new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433 };
        var b = new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433, ServiceTag = "tag" };

        Assert.False(TunnelConfigComparer.Equals(a, b));
    }

    [Fact]
    public void ServiceEquals_IdenticalConfigs_ReturnsTrue()
    {
        var a = new ExposedServiceConfig { Name = "sql", TargetAddress = "host:1433", ServiceTag = "tag" };
        var b = new ExposedServiceConfig { Name = "sql", TargetAddress = "host:1433", ServiceTag = "tag" };

        Assert.True(TunnelConfigComparer.ServiceEquals(a, b));
    }

    [Fact]
    public void ServiceEquals_DifferentTarget_ReturnsFalse()
    {
        var a = new ExposedServiceConfig { Name = "sql", TargetAddress = "host1:1433" };
        var b = new ExposedServiceConfig { Name = "sql", TargetAddress = "host2:1433" };

        Assert.False(TunnelConfigComparer.ServiceEquals(a, b));
    }

    [Fact]
    public void ServiceListEquals_SameLists_ReturnsTrue()
    {
        var a = new List<ExposedServiceConfig>
        {
            new() { Name = "sql", TargetAddress = "host:1433" }
        };
        var b = new List<ExposedServiceConfig>
        {
            new() { Name = "sql", TargetAddress = "host:1433" }
        };

        Assert.True(TunnelConfigComparer.ServiceListEquals(a, b));
    }

    [Fact]
    public void ServiceListEquals_DifferentCount_ReturnsFalse()
    {
        var a = new List<ExposedServiceConfig>
        {
            new() { Name = "sql", TargetAddress = "host:1433" }
        };
        var b = new List<ExposedServiceConfig>();

        Assert.False(TunnelConfigComparer.ServiceListEquals(a, b));
    }

    [Fact]
    public void ServiceListEquals_EmptyLists_ReturnsTrue()
    {
        Assert.True(TunnelConfigComparer.ServiceListEquals([], []));
    }
}
