using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using AstralParty.Toys.Mobile.Channels;
using AstralParty.Toys.Mobile.Models;

namespace AstralParty.Toys.Mobile.Services;

/// <summary>
/// 检测移动端连接所需的本机环境：ADB（Android Platform Tools）与 iTunes / Apple Mobile Device Service（usbmuxd），
/// 并支持从当前运行进程列表中自动扫描 <c>adb.exe</c> 路径。
/// </summary>
public static class MobileEnvironmentService
{
    public const string AdbDownloadUrl = "https://developer.android.com/tools/releases/platform-tools";
    public const string ItunesDownloadUrl = "https://www.apple.com/itunes/download/win64";

    public static async Task<MobileEnvironmentReport> CheckAsync(
        string? customAdbPath, CancellationToken cancellationToken = default)
    {
        var adbTask = CheckAdbAsync(customAdbPath, cancellationToken);
        var itunesTask = CheckItunesAsync(cancellationToken);
        await Task.WhenAll(adbTask, itunesTask);

        return new MobileEnvironmentReport
        {
            Adb = adbTask.Result,
            Itunes = itunesTask.Result,
            CheckedAt = DateTimeOffset.Now.ToString("o"),
        };
    }

    /// <summary>
    /// 首先从系统运行中的进程列表扫描 adb（以及已运行的 adb.exe 完整路径）；扫不到时明确返回「找不到」。
    /// </summary>
    public static AdbProcessScanResult ScanAdbFromProcesses(Func<IEnumerable<string?>>? processPathProvider = null)
    {
        var rawPaths = processPathProvider is not null ? processPathProvider() : EnumerateAdbProcessPaths();
        var candidates = rawPaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim())
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            return new AdbProcessScanResult
            {
                Found = false,
                Message = "在当前运行的进程列表中找不到 adb.exe；请先打开手机助手 / 模拟器，或手动指定 adb.exe 路径。",
            };
        }

        return new AdbProcessScanResult
        {
            Found = true,
            Path = candidates[0],
            Candidates = candidates,
            Message = $"已从运行中的进程扫描到 adb：{candidates[0]}",
        };
    }

    private static async Task<AdbEnvironmentStatus> CheckAdbAsync(
        string? customAdbPath, CancellationToken cancellationToken)
    {
        var configured = (customAdbPath ?? "").Trim();
        var (resolved, source, configInvalid) = AndroidChannel.ResolveAdbDetailed(configured);
        var status = new AdbEnvironmentStatus
        {
            Source = source,
            ResolvedPath = source == "missing" ? "" : resolved,
            ConfiguredPath = configured,
            ConfigInvalid = configInvalid,
            DownloadUrl = AdbDownloadUrl,
        };

        if (source == "missing")
        {
            status.Ok = false;
            status.Message = configInvalid
                ? $"指定的 adb 路径不存在：{configured}；系统环境中也未检测到 adb。"
                : "未检测到 adb.exe（Android Platform-Tools）。可从进程自动扫描、手动指定或下载安装。";
            return status;
        }

        var (ok, versionLine, err) = await ProbeAdbVersionAsync(resolved, cancellationToken);
        status.Ok = ok;
        status.Version = versionLine;
        status.Message = ok
            ? (configInvalid ? $"指定路径不存在已回退自动路径（{versionLine}）" : versionLine)
            : $"找到文件但无法执行：{err}";
        return status;
    }

    private static async Task<ItunesEnvironmentStatus> CheckItunesAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new ItunesEnvironmentStatus
            {
                Ok = true,
                Status = "ok",
                Installed = true,
                ServiceRunning = true,
                Message = "非 Windows 环境由系统 usbmuxd 托管",
                DownloadUrl = ItunesDownloadUrl,
            };
        }

        var amdsExe = FirstExistingPath(
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                "Apple", "Mobile Device Support", "AppleMobileDeviceService.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86),
                "Apple", "Mobile Device Support", "AppleMobileDeviceService.exe"),
        ]);
        var itunesExe = FirstExistingPath(
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "iTunes", "iTunes.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "iTunes", "iTunes.exe"),
        ]);

        var serviceQuery = await QueryWindowsServiceAsync("Apple Mobile Device Service", cancellationToken);
        var usbmuxOpen = await ProbeLoopbackPortAsync(27015, cancellationToken);
        var processRunning = Process.GetProcessesByName("AppleMobileDeviceService").Length > 0 ||
                             Process.GetProcessesByName("AppleDevices").Length > 0;

        var serviceRunning = serviceQuery == ServiceState.Running || usbmuxOpen || processRunning;
        var installed = !string.IsNullOrEmpty(amdsExe) ||
                        !string.IsNullOrEmpty(itunesExe) ||
                        serviceQuery != ServiceState.Missing ||
                        usbmuxOpen ||
                        processRunning;

        var status = serviceRunning ? "ok" : installed ? "stopped" : "missing";
        var msg = status switch
        {
            "ok" => usbmuxOpen
                ? "Apple Mobile Device Service / usbmuxd 正在运行"
                : "Apple 移动设备支持进程正在运行",
            "stopped" => "已检测到 iTunes / Apple 驱动组件，但 Apple Mobile Device Service 未运行；请启动服务或打开 iTunes / Apple 设备应用",
            _ => "未检测到 iTunes 或 Apple 移动设备驱动；连接 iPhone 需先安装 iTunes（传统安装包或 Microsoft Store 的 Apple 设备）",
        };

        return new ItunesEnvironmentStatus
        {
            Ok = serviceRunning,
            Status = status,
            Installed = installed,
            ServiceRunning = serviceRunning,
            ServiceName = "Apple Mobile Device Service",
            ExecutablePath = amdsExe ?? itunesExe ?? "",
            Message = msg,
            DownloadUrl = ItunesDownloadUrl,
        };
    }

    private static IEnumerable<string?> EnumerateAdbProcessPaths()
    {
        Process[] list;
        try
        {
            list = Process.GetProcessesByName("adb");
        }
        catch
        {
            yield break;
        }

        foreach (var proc in list)
        {
            using (proc)
            {
                string? path = null;
                try
                {
                    path = proc.MainModule?.FileName;
                }
                catch
                {
                    path = TryQueryProcessImagePath(proc);
                }
                if (!string.IsNullOrEmpty(path))
                    yield return path;
            }
        }
    }

    private static string? TryQueryProcessImagePath(Process proc)
    {
        if (!OperatingSystem.IsWindows())
            return null;
        try
        {
            var sb = new StringBuilder(1024);
            var size = sb.Capacity;
            return QueryFullProcessImageName(proc.Handle, 0, sb, ref size) ? sb.ToString(0, size) : null;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    private static async Task<(bool Ok, string VersionLine, string Error)> ProbeAdbVersionAsync(
        string adbPath, CancellationToken cancellationToken)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = adbPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            info.ArgumentList.Add("version");
            using var proc = Process.Start(info) ?? throw new IOException("无法启动 adb");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            var stdout = await proc.StandardOutput.ReadToEndAsync(cts.Token);
            var stderr = await proc.StandardError.ReadToEndAsync(cts.Token);
            await proc.WaitForExitAsync(cts.Token);
            if (proc.ExitCode != 0)
                return (false, "", (stderr + stdout).Trim());
            var firstLine = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "adb 可用";
            return (true, firstLine, "");
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }

    private enum ServiceState { Missing, Stopped, Running }

    private static async Task<ServiceState> QueryWindowsServiceAsync(
        string serviceName, CancellationToken cancellationToken)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = "sc.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            info.ArgumentList.Add("query");
            info.ArgumentList.Add(serviceName);
            using var proc = Process.Start(info);
            if (proc is null)
                return ServiceState.Missing;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            var text = await proc.StandardOutput.ReadToEndAsync(cts.Token);
            await proc.WaitForExitAsync(cts.Token);
            if (proc.ExitCode != 0)
                return ServiceState.Missing;
            return text.Contains("RUNNING", StringComparison.OrdinalIgnoreCase)
                ? ServiceState.Running
                : ServiceState.Stopped;
        }
        catch
        {
            return ServiceState.Missing;
        }
    }

    private static async Task<bool> ProbeLoopbackPortAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(220));
            await client.ConnectAsync(System.Net.IPAddress.Loopback, port, cts.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static string? FirstExistingPath(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return path;
        }
        return null;
    }
}
