using Microsoft.Win32.SafeHandles;

namespace PortTunneler.ServiceHelper;

public class SafeServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeServiceHandle() : base(true) { }

    public SafeServiceHandle(IntPtr preexistingHandle) : base(true)
    {
        SetHandle(preexistingHandle);
    }

    protected override bool ReleaseHandle()
    {
        return NativeMethods.CloseServiceHandle(handle);
    }
}