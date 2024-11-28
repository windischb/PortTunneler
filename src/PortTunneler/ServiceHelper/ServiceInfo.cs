namespace PortTunneler.ServiceHelper;

public class ServiceInfo
{
    private readonly IServiceInstaller _installer;
    private readonly string _serviceName;

    public string ServiceName => _serviceName;
    public string DisplayName { get; }
    public string ExecutablePath { get; }

    public ServiceInfo(IServiceInstaller installer, string serviceName, string displayName, string executablePath)
    {
        _installer = installer;
        _serviceName = serviceName;
        DisplayName = displayName;
        ExecutablePath = executablePath;
    }

    public void Start()
    {
        _installer.StartService(_serviceName);
    }

    public void Stop()
    {
        _installer.StopService(_serviceName);
    }

    public void Uninstall()
    {
        _installer.Uninstall(_serviceName);
    }

    public ServiceState GetStatus()
    {
        return _installer.GetServiceStatus(_serviceName);
    }
}