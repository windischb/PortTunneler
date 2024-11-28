using System.Runtime.InteropServices;

namespace PortTunneler.ServiceHelper;

public static class ServiceInstallerFactory
{
    public static IServiceInstaller Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsServiceInstaller();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxServiceInstaller(); // To be implemented
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacServiceInstaller(); // To be implemented
        }
        else
        {
            throw new PlatformNotSupportedException("This platform is not supported");
        }
    }
}