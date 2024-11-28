using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace PortTunneler.ServiceHelper;

public class WindowsServiceInstaller : IServiceInstaller
{
    public ServiceInfo InstallAndStart(string serviceName, string displayName, string fileName, string arguments)
    {
        var serviceInfo = Install(serviceName, displayName, fileName, arguments);
        StartService(serviceInfo.ServiceName);
        return serviceInfo;
    }

    public ServiceInfo Install(string serviceName, string displayName, string fileName, string arguments)
    {
        using (SafeServiceHandle scm = OpenSCManager(ScmAccessRights.AllAccess))
        {
            SafeServiceHandle service = OpenServiceHandle(scm, serviceName, ServiceAccessRights.AllAccess);
            if (service.IsInvalid)
            {
                string fullPath = string.IsNullOrWhiteSpace(arguments) ? fileName : $"{fileName} {arguments}";
                IntPtr serviceHandle = NativeMethods.CreateService(scm.DangerousGetHandle(), serviceName, displayName, ServiceAccessRights.AllAccess, NativeMethods.SERVICE_WIN32_OWN_PROCESS, ServiceBootFlag.AutoStart, ServiceError.Normal, fullPath, null, IntPtr.Zero, null, null, null);
                if (serviceHandle == IntPtr.Zero)
                    throw new ApplicationException("Failed to install service.");

                service = new SafeServiceHandle(serviceHandle);
            }

            return new ServiceInfo(this, serviceName, displayName, fileName);
        }
    }


    public void Uninstall(string serviceName)
    {
        using (SafeServiceHandle scm = OpenSCManager(ScmAccessRights.AllAccess))
        {
            using (SafeServiceHandle service = OpenServiceHandle(scm, serviceName, ServiceAccessRights.AllAccess))
            {
                if (service.IsInvalid)
                    throw new ApplicationException("Service not installed.");

                StopService(service.DangerousGetHandle());
                if (!NativeMethods.DeleteService(service.DangerousGetHandle()))
                    throw new ApplicationException("Could not delete service " + Marshal.GetLastWin32Error());
            }
        }
    }

    public bool ServiceIsInstalled(string serviceName)
    {
        return ServiceController.GetServices().Any(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
    }

    public void StartService(string serviceName)
    {
        using (ServiceController service = new ServiceController(serviceName))
        {
            if (service.Status == ServiceControllerStatus.Stopped)
            {
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }
        }
    }

    public void StopService(string serviceName)
    {
        using (ServiceController service = new ServiceController(serviceName))
        {
            if (service.Status == ServiceControllerStatus.Running)
            {
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }
        }
    }

    public ServiceState GetServiceStatus(string serviceName)
    {
        using (ServiceController service = new ServiceController(serviceName))
        {
            return service.Status switch
            {
                ServiceControllerStatus.Running => ServiceState.Running,
                ServiceControllerStatus.Stopped => ServiceState.Stopped,
                ServiceControllerStatus.Paused => ServiceState.Paused,
                ServiceControllerStatus.StartPending => ServiceState.StartPending,
                ServiceControllerStatus.StopPending => ServiceState.StopPending,
                _ => ServiceState.Unknown,
            };
        }
    }

    public ServiceInfo GetServiceByExecutablePath(string executablePath)
    {
        var services = ServiceController.GetServices();
        foreach (var service in services)
        {
            try
            {
                using (SafeServiceHandle scm = OpenSCManager(ScmAccessRights.AllAccess))
                {
                    using (SafeServiceHandle hService = OpenServiceHandle(scm, service.ServiceName, ServiceAccessRights.QueryConfig))
                    {
                        if (!hService.IsInvalid)
                        {
                            string servicePath = GetServiceExecutablePath(hService);
                            if (servicePath.Equals(executablePath, StringComparison.InvariantCultureIgnoreCase))
                            {
                                return new ServiceInfo(this, service.ServiceName, service.DisplayName, servicePath);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignore services we cannot access.
            }
        }
        return null;
    }

    public ServiceInfo GetServiceByName(string serviceName)
    {
        var services = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
        if (services != null)
        {
            using (SafeServiceHandle scm = OpenSCManager(ScmAccessRights.AllAccess))
            {
                using (SafeServiceHandle hService = OpenServiceHandle(scm, serviceName, ServiceAccessRights.QueryConfig))
                {
                    if (!hService.IsInvalid)
                    {
                        string servicePath = GetServiceExecutablePath(hService);
                        return new ServiceInfo(this, serviceName, services.DisplayName, servicePath);
                    }
                }
            }
        }
        return null;
    }

    private void StopService(IntPtr service)
    {
        SERVICE_STATUS status = new SERVICE_STATUS();
        NativeMethods.ControlService(service, ServiceControl.Stop, status);
        WaitForServiceStatus(service, ServiceState.StopPending, ServiceState.Stopped);
    }

    private bool WaitForServiceStatus(IntPtr service, ServiceState waitStatus, ServiceState desiredStatus)
    {
        SERVICE_STATUS status = new SERVICE_STATUS();
        NativeMethods.QueryServiceStatus(service, status);

        if (status.dwCurrentState == desiredStatus) return true;

        int dwStartTickCount = Environment.TickCount;
        int dwOldCheckPoint = status.dwCheckPoint;

        while (status.dwCurrentState == waitStatus)
        {
            int dwWaitTime = status.dwWaitHint / 10;
            if (dwWaitTime < 1000) dwWaitTime = 1000;
            else if (dwWaitTime > 10000) dwWaitTime = 10000;

            Thread.Sleep(dwWaitTime);

            if (NativeMethods.QueryServiceStatus(service, status) == 0) break;

            if (status.dwCheckPoint > dwOldCheckPoint)
            {
                dwStartTickCount = Environment.TickCount;
                dwOldCheckPoint = status.dwCheckPoint;
            }
            else if (Environment.TickCount - dwStartTickCount > status.dwWaitHint)
            {
                break;
            }
        }
        return (status.dwCurrentState == desiredStatus);
    }

    private string GetServiceExecutablePath(SafeServiceHandle service)
    {
        const int QUERY_SERVICE_CONFIG = 0x00000001;
        int bufferSize = 4096;
        IntPtr buffer = Marshal.AllocHGlobal(bufferSize);

        try
        {
            int bytesNeeded;
            if (!NativeMethods.QueryServiceConfig(service.DangerousGetHandle(), buffer, bufferSize, out bytesNeeded))
            {
                if (bytesNeeded > bufferSize)
                {
                    bufferSize = bytesNeeded;
                    buffer = Marshal.ReAllocHGlobal(buffer, (IntPtr)bufferSize);
                    if (!NativeMethods.QueryServiceConfig(service.DangerousGetHandle(), buffer, bufferSize, out bytesNeeded))
                    {
                        throw new ApplicationException("Failed to query service config");
                    }
                }
                else
                {
                    throw new ApplicationException("Failed to query service config");
                }
            }

            var config = (QUERY_SERVICE_CONFIG)Marshal.PtrToStructure(buffer, typeof(QUERY_SERVICE_CONFIG));
            return config.binaryPathName;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private SafeServiceHandle OpenSCManager(ScmAccessRights rights)
    {
        IntPtr scm = NativeMethods.OpenSCManager(null, null, rights);
        if (scm == IntPtr.Zero)
            throw new ApplicationException("Could not connect to service control manager.");

        return new SafeServiceHandle(scm);
    }

    private SafeServiceHandle OpenServiceHandle(SafeServiceHandle scm, string serviceName, ServiceAccessRights rights)
    {
        IntPtr service = NativeMethods.OpenService(scm.DangerousGetHandle(), serviceName, rights);
        return new SafeServiceHandle(service);
    }
}