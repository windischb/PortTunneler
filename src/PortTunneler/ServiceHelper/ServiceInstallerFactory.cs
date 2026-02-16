namespace PortTunneler.ServiceHelper;

public static class ServiceInstallerFactory
{
    public static IServiceInstaller Create() => true switch
    {
        _ when OperatingSystem.IsWindows() => new WindowsServiceInstaller(),
        _ when OperatingSystem.IsLinux() => new LinuxServiceInstaller(),
        _ when OperatingSystem.IsMacOS() => new MacServiceInstaller(),
        _ => throw new PlatformNotSupportedException("This platform is not supported")
    };
}
