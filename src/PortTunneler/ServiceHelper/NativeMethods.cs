using System.Runtime.InteropServices;

namespace PortTunneler.ServiceHelper;

public static class NativeMethods
{
    public const int STANDARD_RIGHTS_REQUIRED = 0xF0000;
    public const int SERVICE_WIN32_OWN_PROCESS = 0x00000010;
    
    [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr OpenSCManager(string machineName, string databaseName, ScmAccessRights dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, ServiceAccessRights dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr CreateService(IntPtr hSCManager, string lpServiceName, string lpDisplayName, ServiceAccessRights dwDesiredAccess, int dwServiceType, ServiceBootFlag dwStartType, ServiceError dwErrorControl, string lpBinaryPathName, string lpLoadOrderGroup, IntPtr lpdwTagId, string lpDependencies, string lp, string lpPassword);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseServiceHandle(IntPtr hSCObject);

    [DllImport("advapi32.dll")]
    public static extern int QueryServiceStatus(IntPtr hService, [Out] SERVICE_STATUS lpServiceStatus);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteService(IntPtr hService);

    [DllImport("advapi32.dll")]
    public static extern int ControlService(IntPtr hService, ServiceControl dwControl, [Out] SERVICE_STATUS lpServiceStatus);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool QueryServiceConfig(IntPtr hService, IntPtr lpServiceConfig, int cbBufSize, out int pcbBytesNeeded);
}

[StructLayout(LayoutKind.Sequential)]
public class SERVICE_STATUS
{
    public int dwServiceType = 0;
    public ServiceState dwCurrentState = 0;
    public int dwControlsAccepted = 0;
    public int dwWin32ExitCode = 0;
    public int dwServiceSpecificExitCode = 0;
    public int dwCheckPoint = 0;
    public int dwWaitHint = 0;
}

[StructLayout(LayoutKind.Sequential)]
public struct QUERY_SERVICE_CONFIG
{
    public uint serviceType;
    public uint startType;
    public uint errorControl;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string binaryPathName;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string loadOrderGroup;
    public uint tagId;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string dependencies;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string serviceStartName;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string displayName;
}