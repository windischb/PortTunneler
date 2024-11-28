namespace PortTunneler.ServiceHelper;

public interface IServiceInstaller
{
    ServiceInfo InstallAndStart(string serviceName, string displayName, string fileName, string arguments);
    ServiceInfo Install(string serviceName, string displayName, string fileName, string arguments);
    void Uninstall(string serviceName);
    bool ServiceIsInstalled(string serviceName);
    void StartService(string serviceName);
    void StopService(string serviceName);
    ServiceState GetServiceStatus(string serviceName);
    ServiceInfo GetServiceByExecutablePath(string executablePath);
    ServiceInfo GetServiceByName(string serviceName);
}