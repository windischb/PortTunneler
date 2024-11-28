using System.Diagnostics;

namespace PortTunneler.ServiceHelper;

public class MacServiceInstaller : IServiceInstaller
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
        ExecuteLaunchdCommand($"start {serviceName}");
    }

    public void StopService(string serviceName)
    {
        ExecuteLaunchdCommand($"stop {serviceName}");
    }

    public ServiceState GetServiceStatus(string serviceName)
    {
        var output = ExecuteLaunchdCommand($"list | grep {serviceName}");
        return string.IsNullOrEmpty(output) ? ServiceState.Stopped : ServiceState.Running;
    }

    public ServiceInfo GetServiceByExecutablePath(string executablePath)
    {
        var services = Directory.GetFiles(LaunchdServicePath, "*.plist");
        foreach (var serviceFile in services)
        {
            string serviceFilePath = Path.Combine(LaunchdServicePath, serviceFile);
            if (File.Exists(serviceFilePath))
            {
                string content = File.ReadAllText(serviceFilePath);
                if (content.Contains(executablePath))
                {
                    string serviceName = Path.GetFileNameWithoutExtension(serviceFile);
                    return new ServiceInfo(this, serviceName, serviceName, executablePath);
                }
            }
        }
        return null;
    }

    public ServiceInfo GetServiceByName(string serviceName)
    {
        var serviceFilePath = GetServiceFilePath(serviceName);
        if (File.Exists(serviceFilePath))
        {
            string content = File.ReadAllText(serviceFilePath);
            string executablePath = ParseExecutablePath(content);
            return new ServiceInfo(this, serviceName, serviceName, executablePath);
        }
        return null;
    }

    private void CreateServiceFile(string serviceName, string displayName, string fileName, string arguments)
    {
        var argumentsArray = string.IsNullOrWhiteSpace(arguments) ? string.Empty : $"<string>{arguments}</string>";

        var plistContent = $@"
<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
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
    <string>root</string>
</dict>
</plist>";

        File.WriteAllText(GetServiceFilePath(serviceName), plistContent);
    }


    private void LoadService(string serviceName)
    {
        ExecuteLaunchdCommand($"load -w {GetServiceFilePath(serviceName)}");
    }

    private void LoadAndStartService(string serviceName)
    {
        ExecuteLaunchdCommand($"load -w {GetServiceFilePath(serviceName)}");
        ExecuteLaunchdCommand($"start {serviceName}");
    }

    private void UnloadService(string serviceName)
    {
        ExecuteLaunchdCommand($"unload {GetServiceFilePath(serviceName)}");
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

    private string ParseExecutablePath(string content)
    {
        var execStartLine = content.Split('\n').FirstOrDefault(line => line.Trim().StartsWith("<string>"));
        if (execStartLine != null)
        {
            var parts = execStartLine.Split('<', '>');
            if (parts.Length > 2)
            {
                return parts[2].Trim();
            }
        }
        return null;
    }

    private string ExecuteLaunchdCommand(string command)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sh",
                Arguments = $"-c \"launchctl {command}\"",
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
            throw new InvalidOperationException($"Command 'launchctl {command}' failed with error: {error}");
        }

        return output;
    }
}