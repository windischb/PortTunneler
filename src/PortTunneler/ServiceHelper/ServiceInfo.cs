namespace PortTunneler.ServiceHelper;

public sealed record ServiceInfo(IServiceInstaller Installer, string ServiceName, string DisplayName, string ExecutablePath)
{
    public void Start()
    {
        Installer.StartService(ServiceName);
    }

    public void Stop()
    {
        Installer.StopService(ServiceName);
    }

    public void Uninstall()
    {
        Installer.Uninstall(ServiceName);
    }

    public ServiceState GetStatus()
    {
        return Installer.GetServiceStatus(ServiceName);
    }
}
