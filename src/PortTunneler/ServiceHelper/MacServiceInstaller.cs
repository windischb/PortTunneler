using System.Diagnostics;

namespace PortTunneler.ServiceHelper;

public sealed class MacServiceInstaller : IServiceInstaller
{
    private const string LaunchdServicePath = "/Library/LaunchDaemons";

    public ServiceInfo InstallAndStart(string serviceName, string displayName, string fileName, string arguments)
    {
        var serviceInfo = Install(serviceName, displayName, fileName, arguments);
        StartService(serviceInfo.ServiceName);
        return serviceInfo;
    }

    public ServiceInfo Install(string serviceName, string displayName, string fileName, string arguments)
    {
        CreateServiceFile(serviceName, displayName, fileName, arguments);
        LoadService(serviceName);
        return new ServiceInfo(this, serviceName, displayName, fileName);
    }

    public void Uninstall(string serviceName)
    {
        UnloadService(serviceName);
        RemoveServiceFile(serviceName);
    }

    public bool ServiceIsInstalled(string serviceName)
    {
        return File.Exists(GetServiceFilePath(serviceName));
    }

    public void StartService(string serviceName)
    {
        ExecuteLaunchdCommand("start", serviceName);
    }

    public void StopService(string serviceName)
    {
        ExecuteLaunchdCommand("stop", serviceName);
    }

    public ServiceState GetServiceStatus(string serviceName)
    {
        try
        {
            ExecuteLaunchdCommand("list", serviceName);
            return ServiceState.Running;
        }
        catch (InvalidOperationException)
        {
            return ServiceState.Stopped;
        }
    }

    public ServiceInfo? GetServiceByExecutablePath(string executablePath)
    {
        var services = Directory.GetFiles(LaunchdServicePath, "*.plist");
        foreach (var serviceFile in services)
        {
            if (File.Exists(serviceFile))
            {
                string content = File.ReadAllText(serviceFile);
                if (content.Contains(executablePath))
                {
                    string serviceName = Path.GetFileNameWithoutExtension(serviceFile);
                    return new ServiceInfo(this, serviceName, serviceName, executablePath);
                }
            }
        }
        return null;
    }

    public ServiceInfo? GetServiceByName(string serviceName)
    {
        var serviceFilePath = GetServiceFilePath(serviceName);
        if (File.Exists(serviceFilePath))
        {
            string content = File.ReadAllText(serviceFilePath);
            string? executablePath = ParseExecutablePath(content);
            if (executablePath != null)
            {
                return new ServiceInfo(this, serviceName, serviceName, executablePath);
            }
        }
        return null;
    }

    private void CreateServiceFile(string serviceName, string displayName, string fileName, string arguments)
    {
        var argumentsArray = string.IsNullOrWhiteSpace(arguments) ? string.Empty : $"<string>{arguments}</string>";

        var plistContent = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>{serviceName}</string>
                <key>ProgramArguments</key>
                <array>
                    <string>{fileName}</string>
                    {argumentsArray}
                </array>
                <key>RunAtLoad</key>
                <true/>
                <key>KeepAlive</key>
                <dict>
                    <key>SuccessfulExit</key>
                    <false/>
                    <key>AfterInitialDemand</key>
                    <true/>
                </dict>
                <key>UserName</key>
                <string>{Environment.UserName}</string>
            </dict>
            </plist>
            """;

        File.WriteAllText(GetServiceFilePath(serviceName), plistContent);
    }

    private void LoadService(string serviceName)
    {
        ExecuteLaunchdCommand("load", $"-w {GetServiceFilePath(serviceName)}");
    }

    private void UnloadService(string serviceName)
    {
        ExecuteLaunchdCommand("unload", GetServiceFilePath(serviceName));
    }

    private void RemoveServiceFile(string serviceName)
    {
        var serviceFilePath = GetServiceFilePath(serviceName);
        if (File.Exists(serviceFilePath))
        {
            File.Delete(serviceFilePath);
        }
    }

    private string GetServiceFilePath(string serviceName)
    {
        return Path.Combine(LaunchdServicePath, $"{serviceName}.plist");
    }

    private string? ParseExecutablePath(string content)
    {
        var lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "<key>ProgramArguments</key>")
            {
                for (int j = i + 1; j < lines.Length; j++)
                {
                    var trimmed = lines[j].Trim();
                    if (trimmed.StartsWith("<string>") && trimmed.EndsWith("</string>"))
                    {
                        return trimmed["<string>".Length..^"</string>".Length];
                    }
                    if (trimmed == "</array>")
                    {
                        break;
                    }
                }
                break;
            }
        }
        return null;
    }

    private string ExecuteLaunchdCommand(string command, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "launchctl",
                Arguments = $"{command} {arguments}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Command 'launchctl {command} {arguments}' failed with error: {error}");
        }

        return output;
    }
}
