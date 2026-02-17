using System.Net;

namespace PortTunneler;

internal static class WslHelper
{
    private static readonly Lazy<bool> IsWslLazy = new(DetectWsl);
    private static readonly Lazy<IPAddress?> HostAddressLazy = new(ResolveHostAddress);

    public static bool IsWsl => IsWslLazy.Value;

    public static IPAddress? GetHostAddress() => HostAddressLazy.Value;

    private static bool DetectWsl()
    {
        if (!OperatingSystem.IsLinux())
            return false;

        try
        {
            var version = File.ReadAllText("/proc/version");
            return version.Contains("microsoft", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static IPAddress? ResolveHostAddress()
    {
        if (!IsWsl)
            return null;

        try
        {
            foreach (var line in File.ReadLines("/etc/resolv.conf"))
            {
                if (!line.StartsWith("nameserver", StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && IPAddress.TryParse(parts[1], out var address))
                    return address;
            }
        }
        catch
        {
            // Swallow — not critical
        }

        return null;
    }
}
