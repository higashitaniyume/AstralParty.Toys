using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AstralParty.Toys.Mobile.Models;

namespace AstralParty.Toys.Mobile.Channels;

/// <summary>
/// adb channel. Replicates the shell identity used by Shizuku and the cesium-deploy.ps1 scripts.
/// Reads use exec-out/shell; writes go over stdin into a &lt;path&gt;.cesium-tmp sibling and are then
/// moved into place, so a failed transfer never leaves a zero-byte file behind.
/// </summary>
public sealed class AndroidChannel : IDeviceChannel
{
    public string Kind => "android";

    public string BuildRoot(MobileTarget target) => $"/sdcard/Android/data/{target.AppId}/files/";

    /// <summary>Appends the scheduler-style temp suffix used for every staged write.</summary>
    public const string TempSuffix = ".cesium-tmp";

    /// <summary>
    /// Single-quotes a path for <c>adb shell</c>. Embedded quotes are escaped the POSIX way
    /// (<c>'</c> closes the literal, <c>\'</c> adds one, the next <c>'</c> reopens it) so a path can
    /// never break out of the quoting and become a command.
    /// </summary>
    public static string Quote(string path) => "'" + path.Replace("'", "'\\''") + "'";

    public async Task<IReadOnlyList<DeviceInfo>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var output = await RunTextAsync(["devices", "-l"], cancellationToken);
        var result = new List<DeviceInfo>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase))
                continue;
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2 || !string.Equals(fields[1], "device", StringComparison.OrdinalIgnoreCase))
                continue;

            var property = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in fields.Skip(2))
            {
                var parts = field.Split(':', 2);
                if (parts.Length == 2)
                    property[parts[0]] = parts[1];
            }

            var version = await RunTextAsync(["-s", fields[0], "shell", "getprop ro.build.version.release"], cancellationToken);
            result.Add(new DeviceInfo
            {
                Kind = "android",
                Id = fields[0],
                Label = property.GetValueOrDefault("model") ?? property.GetValueOrDefault("product") ?? fields[0],
                Model = property.GetValueOrDefault("product") ?? "",
                OsVersion = version.Trim(),
                Paired = true,
                State = "online",
                LastSeen = DateTimeOffset.Now.ToString("o"),
            });
        }

        return result;
    }

    public Task<IDeviceFileSystem> OpenAsync(MobileTarget target, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(target.AppId))
            throw new InvalidOperationException("安卓设备需要指定包名");
        return Task.FromResult<IDeviceFileSystem>(new AdbFileSystem(target.Serial));
    }

    /// <summary>
    /// Read-only capability probe that creates nothing: can the adb shell identity write
    /// <paramref name="path"/>? The app's own identity cannot reach another app's Android/data, but
    /// <c>shell</c> is in the <c>ext_data_rw</c> group, so the desktop tool does not need Shizuku.
    /// Callers should gate device writes on this instead of discovering it through a failed write.
    /// </summary>
    public async Task<bool> CanWriteAsync(MobileTarget target, string path, CancellationToken cancellationToken = default)
    {
        var text = await RunTextAsync(
            ["-s", target.Serial, "shell", $"test -w {Quote(path)} && echo 1 || echo 0"], cancellationToken);
        return text.Trim() == "1";
    }

    /// <summary>
    /// `pm list packages` 里按名字特征挑候选，再逐个看私有目录与 Addressables 缓存是否存在。
    /// 用目录存在性判断「真的装了且进过游戏」，比只认包名可靠。
    /// </summary>
    public async Task<IReadOnlyList<MobilePackageCandidate>> FindGamePackagesAsync(
        string serial, CancellationToken cancellationToken = default)
    {
        var result = new List<MobilePackageCandidate>();
        var listing = await RunTextAsync(["-s", serial, "shell", "pm list packages"], cancellationToken);
        foreach (var line in listing.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("package:", StringComparison.Ordinal))
                continue;
            var appId = trimmed["package:".Length..].Trim();
            if (appId.Length == 0)
                continue;
            var matchedBy = MobilePaths.MatchGameIdMarker(appId);
            if (matchedBy is null)
                continue;

            var files = $"/sdcard/Android/data/{appId}/files";
            var candidate = new MobilePackageCandidate
            {
                AppId = appId,
                Name = appId,
                Version = await ReadVersionNameAsync(serial, appId, cancellationToken),
                MatchedBy = matchedBy,
                ContainerAccessible = await TestAsync(serial, $"-d {Quote(files)}", cancellationToken),
            };
            candidate.HasCache = candidate.ContainerAccessible &&
                await TestAsync(serial, $"-d {Quote($"{files}/{MobilePaths.AddressablesRelative}")}", cancellationToken);
            result.Add(candidate);
        }

        return result;
    }

    private static async Task<bool> TestAsync(string serial, string expression, CancellationToken cancellationToken)
    {
        var text = await RunTextAsync(
            ["-s", serial, "shell", $"test {expression} && echo 1 || echo 0"], cancellationToken);
        return text.Trim() == "1";
    }

    private static async Task<string> ReadVersionNameAsync(string serial, string appId,
        CancellationToken cancellationToken)
    {
        try
        {
            var text = await RunTextAsync(
                ["-s", serial, "shell", $"dumpsys package {Quote(appId)} | grep -m1 versionName"], cancellationToken);
            var match = Regex.Match(text, @"versionName=(\S+)");
            return match.Success ? match.Groups[1].Value : "";
        }
        catch (Exception)
        {
            // 取不到版本不影响候选列表
            return "";
        }
    }

    private static string ResolveAdb()
    {
        foreach (var root in new[]
                 {
                     Environment.GetEnvironmentVariable("ANDROID_HOME"),
                     Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
                     Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) is { Length: > 0 } local
                         ? Path.Combine(local, "Android", "Sdk")
                         : null,
                 })
        {
            if (string.IsNullOrEmpty(root))
                continue;
            var candidate = Path.Combine(root, "platform-tools", OperatingSystem.IsWindows() ? "adb.exe" : "adb");
            if (File.Exists(candidate))
                return candidate;
        }

        return "adb";
    }

    private static async Task<string> RunTextAsync(string[] args, CancellationToken cancellationToken)
    {
        var (_, stdout, stderr) = await RunAsync(args, cancellationToken);
        if (!string.IsNullOrWhiteSpace(stderr) &&
            stderr.Contains("error:", StringComparison.OrdinalIgnoreCase))
            throw new IOException("adb: " + stderr.Trim());
        return Encoding.UTF8.GetString(stdout);
    }

    private static async Task<byte[]> RunBytesAsync(string[] args, CancellationToken cancellationToken)
    {
        var (_, stdout, _) = await RunAsync(args, cancellationToken);
        return stdout;
    }

    private static async Task<(int ExitCode, byte[] Stdout, string Stderr)> RunAsync(
        string[] args, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo
        {
            FileName = ResolveAdb(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
            info.ArgumentList.Add(arg);

        using var process = Process.Start(info) ?? throw new IOException("无法启动 adb");
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var memory = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(memory, cancellationToken);
        var stderr = await errorTask;
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, memory.ToArray(), stderr);
    }

    private sealed class AdbFileSystem : IDeviceFileSystem
    {
        private readonly string _serial;

        public AdbFileSystem(string serial)
        {
            _serial = serial;
        }

        public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) =>
            (await RunShellAsync($"test -e '{path}' && echo 1 || echo 0", cancellationToken)).Trim() == "1";

        public async Task<DeviceFileInfo?> StatAsync(string path, CancellationToken cancellationToken = default)
        {
            var text = await RunShellAsync($"stat -c '%s %Y' '{path}'", cancellationToken);
            var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !long.TryParse(parts[0], out var size) || !long.TryParse(parts[1], out var seconds))
                return null;
            var modified = seconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(seconds) : (DateTimeOffset?)null;
            return new DeviceFileInfo(size, modified);
        }

        public async Task<IReadOnlyList<string>> ListAsync(string path, CancellationToken cancellationToken = default)
        {
            var text = await RunShellAsync($"ls -1 '{path}'", cancellationToken);
            return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.TrimEnd('\r'))
                .Where(line => line.Length > 0)
                .ToList();
        }

        async Task<string?> IDeviceFileSystem.FindBundleOuterAsync(
            string bundlesRoot, string inner, CancellationToken cancellationToken)
        {
            var text = await RunShellAsync(
                $"find '{bundlesRoot}' -maxdepth 2 -type d -name '{inner}' -print -quit", cancellationToken);
            var path = text.Trim().Replace('\\', '/');
            if (path.Length == 0)
                return null;
            return path[..path.LastIndexOf('/')].Split('/').LastOrDefault();
        }

        public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) =>
            RunBytesAsync(["-s", _serial, "exec-out", "cat", path], cancellationToken);

        public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
            Encoding.UTF8.GetString(await ReadAllBytesAsync(path, cancellationToken));

        /// <remarks>
        /// 两条传输约束都是踩出来的：
        /// ① 二进制不能走 stdin —— Windows 的 adb 按文本模式读 stdin，遇到 <c>0x1A</c>(CTRL+Z) 就当流结束
        ///    （64 KiB 全字节值载荷只落了 26 字节）；
        /// ② <c>adb push</c> 用的是 adbd 的 sync 服务，**写不进别的应用的 Android/data**（Permission denied），
        ///    而 <c>adb shell</c> 跑在 shell 域、带 ext_data_rw 组，是能写的。
        /// 所以统一：先 push 到 <c>/sdcard/Download</c>，再用 shell 落位。
        ///
        /// 落位分两种，取决于目标目录是不是「游戏自己建的」：
        /// <list type="bullet">
        /// <item>可写目录（<c>drwxrws---</c>，组可写，例如 <c>files/</c>、<c>AssetBundles/</c>）：
        ///       写 <c>&lt;path&gt;.cesium-tmp</c> → 比大小 → <c>mv -f</c>，原子且失败不留半截。</item>
        /// <item>不可写目录（<c>drwxr-s---</c>，Unity 建的 <c>&lt;outer&gt;/&lt;inner&gt;/</c>）：
        ///       shell 建不了新文件，但 <c>__data</c> 本身是 <c>-rw-rw----</c>（组可写），
        ///       于是**原地覆盖**，写完立刻核对大小。这里的回滚靠上层在改之前存的本地备份。</item>
        /// </list>
        /// </remarks>
        public async Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default)
        {
            var local = Path.Combine(Path.GetTempPath(), "cesium-push-" + Guid.NewGuid().ToString("N") + ".bin");
            var staging = $"/sdcard/Download/cesium-push-{Guid.NewGuid():N}.bin";
            var temp = path + TempSuffix;
            var canStage = await CanWriteDirectoryAsync(ParentOf(path), cancellationToken);
            try
            {
                await File.WriteAllBytesAsync(local, data, cancellationToken);
                await RunShellCheckedAsync($"rm -f {Quote(staging)}", cancellationToken);

                try
                {
                    await PushAsync(local, staging, cancellationToken);

                    if (canStage)
                    {
                        await RunShellCheckedAsync($"rm -f {Quote(temp)}", cancellationToken);
                        await RunShellCheckedAsync($"cp {Quote(staging)} {Quote(temp)}", cancellationToken);
                        var staged = await SizeAsync(temp, cancellationToken);
                        if (staged != data.Length)
                            throw new IOException($"写入后大小不符：{path}（盘上 {staged}，期望 {data.Length}）");
                        await RunShellCheckedAsync($"mv -f {Quote(temp)} {Quote(path)}", cancellationToken);
                    }
                    else
                    {
                        if (!await ExistsAsync(path, cancellationToken))
                            throw new IOException($"目录不允许新建文件，目标也不存在，无法写入：{path}");

                        // 原地覆盖：不新建任何文件，只覆写已存在的可写文件
                        await RunShellCheckedAsync($"cp {Quote(staging)} {Quote(path)}", cancellationToken);
                        var written = await SizeAsync(path, cancellationToken);
                        if (written != data.Length)
                            throw new IOException($"原地写入后大小不符：{path}（盘上 {written}，期望 {data.Length}）");
                    }
                }
                catch
                {
                    await TryRemoveAsync(temp);
                    throw;
                }
                finally
                {
                    await TryRemoveAsync(staging);
                }
            }
            finally
            {
                try { File.Delete(local); }
                catch (IOException) { /* temp file cleanup is best effort */ }
            }
        }

        public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            await RunShellCheckedAsync($"rm -rf {Quote(path)}", cancellationToken);
            if (await ExistsAsync(path, cancellationToken))
                throw new IOException($"删除失败：{path}");
        }

        public async Task MakeDirectoriesAsync(string path, CancellationToken cancellationToken = default)
        {
            await RunShellCheckedAsync($"mkdir -p {Quote(path)}", cancellationToken);
            var isDir = (await RunShellAsync($"test -d {Quote(path)} && echo 1 || echo 0", cancellationToken)).Trim() == "1";
            if (!isDir)
                throw new IOException($"建目录失败：{path}");
        }

        /// <remarks>
        /// Refuses when the destination already exists. A plain <c>mv</c> onto an existing directory
        /// nests the source inside it (the "装完多嵌一层目录" failure), so callers get an error instead.
        /// </remarks>
        public async Task RenameAsync(string from, string to, CancellationToken cancellationToken = default)
        {
            if (await ExistsAsync(to, cancellationToken))
                throw new IOException($"目标已存在，拒绝改名以免嵌套一层目录：{to}");
            await RunShellCheckedAsync($"mv {Quote(from)} {Quote(to)}", cancellationToken);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private Task<string> RunShellAsync(string script, CancellationToken cancellationToken) =>
            RunTextAsync(["-s", _serial, "shell", script], cancellationToken);

        /// <summary>Runs a shell script and fails on a non-zero remote exit code (adb forwards it).</summary>
        private async Task RunShellCheckedAsync(string script, CancellationToken cancellationToken)
        {
            var (exitCode, stdout, stderr) = await RunAsync(["-s", _serial, "shell", script], cancellationToken);
            if (exitCode != 0)
                throw new IOException($"adb shell 失败（退出码 {exitCode}）：{(stderr + stdout).Trim()}");
        }

        private async Task PushAsync(string localPath, string remotePath, CancellationToken cancellationToken)
        {
            var (exitCode, stdout, stderr) = await RunAsync(["-s", _serial, "push", localPath, remotePath], cancellationToken);
            if (exitCode != 0)
                throw new IOException($"adb push 失败（退出码 {exitCode}）：{(stderr + stdout).Trim()}");
        }

        private static string ParentOf(string path)
        {
            var trimmed = path.TrimEnd('/');
            var slash = trimmed.LastIndexOf('/');
            return slash <= 0 ? "/" : trimmed[..slash];
        }

        private async Task<bool> CanWriteDirectoryAsync(string path, CancellationToken cancellationToken)
        {
            var text = await RunShellAsync($"test -w {Quote(path)} && echo 1 || echo 0", cancellationToken);
            return text.Trim() == "1";
        }

        private async Task<long> SizeAsync(string path, CancellationToken cancellationToken)
        {
            var text = (await RunShellAsync($"stat -c '%s' {Quote(path)}", cancellationToken)).Trim();
            return long.TryParse(text, out var size) ? size : -1;
        }

        private async Task TryRemoveAsync(string path)
        {
            try
            {
                await RunShellCheckedAsync($"rm -f {Quote(path)}", CancellationToken.None);
            }
            catch (Exception)
            {
                // best effort: the caller is already failing for another reason
            }
        }
    }
}
