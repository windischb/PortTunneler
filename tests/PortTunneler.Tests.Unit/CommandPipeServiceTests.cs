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
}
