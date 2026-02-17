using System.Diagnostics;

namespace PortTunneler.ServiceHelper;

public sealed class LinuxServiceInstaller : IServiceInstaller
{
    private const string SystemdServicePath = "/etc/systemd/system";

    public ServiceInfo InstallAndStart(string serviceName, string displayName, string fileName, string arguments)
    {
        var serviceInfo = Install(serviceName, displayName, fileName, arguments);
        StartService(serviceInfo.ServiceName);
        return serviceInfo;
    }

    public ServiceInfo Install(string serviceName, string displayName, string fileName, string arguments)
    {
        CreateServiceFile(serviceName, displayName, fileName, arguments);
        ExecuteSystemdCommand("daemon-reload");
        EnableService(serviceName);
        return new ServiceInfo(this, serviceName, displayName, fileName);
    }

    public void Uninstall(string serviceName)
    {
        StopAndDisableService(serviceName);
        RemoveServiceFile(serviceName);
        ExecuteSystemdCommand("daemon-reload");
    }

    public bool ServiceIsInstalled(string serviceName)
    {
        return File.Exists(GetServiceFilePath(serviceName));
    }

    public void StartService(string serviceName)
    {
        ExecuteSystemdCommand($"start {serviceName}");
    }

    public void StopService(string serviceName)
    {
        ExecuteSystemdCommand($"stop {serviceName}");
    }

    public ServiceState GetServiceStatus(string serviceName)
    {
        var output = ExecuteSystemdCommand($"is-active {serviceName}");
        return output.Trim() switch
        {
            "active" => ServiceState.Running,
            "inactive" => ServiceState.Stopped,
            "activating" => ServiceState.StartPending,
            "deactivating" => ServiceState.StopPending,
            _ => ServiceState.Unknown,
        };
    }

    public ServiceInfo? GetServiceByExecutablePath(string executablePath)
    {
        var services = Directory.GetFiles(SystemdServicePath, "*.service");
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
        string execStart = string.IsNullOrWhiteSpace(arguments) ? fileName : $"{fileName} {arguments}";
        string workingDirectory = Path.GetDirectoryName(fileName) ?? "/";

        var serviceFileContent = $"""
            [Unit]
            Description={displayName}

            [Service]
            Type=notify
            ExecStart={execStart}
            WorkingDirectory={workingDirectory}
            Restart=always
            User={Environment.UserName}

            [Install]
            WantedBy=multi-user.target
            """;

        File.WriteAllText(GetServiceFilePath(serviceName), serviceFileContent);
    }

    private void EnableService(string serviceName)
    {
        ExecuteSystemdCommand($"enable {serviceName}");
    }

    private void StopAndDisableService(string serviceName)
    {
        ExecuteSystemdCommand($"stop {serviceName}");
        ExecuteSystemdCommand($"disable {serviceName}");
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
        return Path.Combine(SystemdServicePath, $"{serviceName}.service");
    }

    private string? ParseExecutablePath(string content)
    {
        var execStartLine = content.Split('\n').FirstOrDefault(line => line.Trim().StartsWith("ExecStart"));
        if (execStartLine != null)
        {
            var parts = execStartLine.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                var value = parts[1].Trim();
                var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return tokens.Length > 0 ? tokens[0] : null;
            }
        }
        return null;
    }

    private string ExecuteSystemdCommand(string command)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = command,
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
            throw new InvalidOperationException($"Command 'systemctl {command}' failed with error: {error}");
        }

        return output;
    }
}
