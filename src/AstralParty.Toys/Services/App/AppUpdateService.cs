using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace AstralParty.Toys.Services.App;

/// <summary>
/// 客户端更新检查与自动更新服务。
/// 通过静态 version.json 文件获取最新 Release 信息，不请求 GitHub REST API，免除配额限制与鉴权。
/// 支持前缀镜像源加速（如 gh-proxy.com）。
/// </summary>
public sealed class AppUpdateService
{
    public const string DefaultRepo = "higashitaniyume/AstralParty.Toys";
    public const string LatestManifestAsset = "version.json";

    public static string LatestManifestUrl(string repo = DefaultRepo) =>
        $"https://github.com/{repo}/releases/latest/download/{LatestManifestAsset}";

    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AppUpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>
    /// 获取当前环境信息。
    /// </summary>
    public AppEnvironmentInfo GetEnvironment() => AppRuntimeDetector.Detect();

    /// <summary>
    /// 从静态 Release Asset (version.json) 检查是否有新版本。
    /// </summary>
    public async Task<(bool HasUpdate, AppVersionManifest? Manifest, AppAssetPackageInfo? TargetPackage, string? Error)> CheckUpdateAsync(
        string? currentVersion = null,
        string? mirrorType = null,
        string? customMirrorUrl = null,
        string repo = DefaultRepo,
        CancellationToken cancellationToken = default)
    {
        currentVersion ??= AppInfo.Get().Version;
        var rawUrl = LatestManifestUrl(repo);
        var fetchUrl = GitHubMirrorHelper.ApplyMirror(rawUrl, mirrorType, customMirrorUrl);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, fetchUrl);
            request.Headers.Add("User-Agent", "AstralParty.Toys-Updater");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (false, null, null, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<AppVersionManifest>(json, JsonOptions);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                return (false, null, null, "未能从 version.json 中解析到有效的版本信息。");
            }

            var hasUpdate = IsNewerVersion(currentVersion, manifest.Version);
            var env = GetEnvironment();

            // 根据当前运行形态选取最匹配的目标安装包
            var targetPackage = SelectTargetPackage(manifest, env.RuntimeKind);

            return (hasUpdate, manifest, targetPackage, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (false, null, null, ex.Message);
        }
    }

    /// <summary>
    /// 根据运行时形态选择对应的安装包资产信息。
    /// </summary>
    public static AppAssetPackageInfo? SelectTargetPackage(AppVersionManifest manifest, AppRuntimeKind runtimeKind)
    {
        if (manifest.Packages == null) return null;

        var selected = runtimeKind switch
        {
            AppRuntimeKind.FrameworkDependent => manifest.Packages.FrameworkDependentInstaller,
            AppRuntimeKind.SelfContained => manifest.Packages.SelfContainedInstaller,
            _ => manifest.Packages.SelfContainedInstaller
        };

        // 容错兜底：若匹配项不存在，返回任意一个存在的安装包
        return selected
            ?? manifest.Packages.SelfContainedInstaller
            ?? manifest.Packages.FrameworkDependentInstaller;
    }

    /// <summary>
    /// 按当前发布形态决定这次怎么更新：安装版跑安装程序，单文件版换 exe，便携版覆盖文件夹。
    /// 做不到（调试版、新版本没有对应的包、没有写入权限）时返回 Manual 和原因，由用户选择改用安装版或自己下载。
    /// switchToInstaller：用户主动选了「改用安装版」。
    /// </summary>
    public static AppUpdatePlan PlanUpdate(AppVersionManifest manifest, AppEnvironmentInfo env, bool switchToInstaller, Func<string, bool>? canWrite = null)
    {
        var installer = SelectTargetPackage(manifest, env.RuntimeKind);
        if (env.PackageForm == AppPackageForm.Development)
            return AppUpdatePlan.Manual("这是调试版或编译输出，不会自动更新。");
        if (env.PackageForm == AppPackageForm.Installer || switchToInstaller)
            return installer != null
                ? new AppUpdatePlan(installer, AppUpdateMode.Installer, "")
                : AppUpdatePlan.Manual("这个版本的发布信息里没有安装包。");

        var p = manifest.Packages;
        var selfContained = env.RuntimeKind == AppRuntimeKind.SelfContained;
        var (package, mode, name) = env.PackageForm == AppPackageForm.SingleFile
            ? (selfContained ? p?.SelfContainedSingleFile : p?.FrameworkDependentSingleFile, AppUpdateMode.ReplaceExe, "单文件版")
            : (selfContained ? p?.SelfContainedPortable : p?.FrameworkDependentPortable, AppUpdateMode.ReplaceFolder, "便携版");
        if (package == null || string.IsNullOrWhiteSpace(package.Url))
            return AppUpdatePlan.Manual($"这个版本的发布信息里没有{name}的下载包，不能原地替换。");

        var directory = Path.GetDirectoryName(env.ExePath) ?? "";
        if (!(canWrite ?? CanWriteDirectory)(directory))
            return AppUpdatePlan.Manual("程序所在的文件夹没有写入权限（比如放在 Program Files 里），不能原地替换。");
        return new AppUpdatePlan(package, mode, "");
    }

    /// <summary>新版本的发布页（手动下载用）。</summary>
    public static string ReleasePageUrl(AppVersionManifest? manifest, string repo = DefaultRepo) =>
        string.IsNullOrWhiteSpace(manifest?.ReleaseTag)
            ? $"https://github.com/{repo}/releases/latest"
            : $"https://github.com/{repo}/releases/tag/{Uri.EscapeDataString(manifest.ReleaseTag)}";

    /// <summary>试着在目录里建一个临时文件，能建就算有写入权限。</summary>
    public static bool CanWriteDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return false;
        var probe = Path.Combine(directory, $".astralparty-toys-write-test-{Guid.NewGuid():N}");
        try
        {
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 把下载好的便携版 zip 解压到临时文件夹，并确认里面真是便携版（有主程序 exe 和 dll），返回解压目录。
    /// </summary>
    public static string ExtractPortablePackage(string zipPath, string? stagingDirectory = null)
    {
        var staging = stagingDirectory ?? Path.Combine(Path.GetTempPath(), "AstralPartyToysUpdate", "portable-" + Path.GetFileNameWithoutExtension(zipPath));
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        Directory.CreateDirectory(staging);
        // .NET 的 ExtractToDirectory 会拒绝跳出目标目录的条目（../ 之类）
        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, staging);
        if (!File.Exists(Path.Combine(staging, "AstralParty.Toys.exe")) || !File.Exists(Path.Combine(staging, "AstralParty.Toys.dll")))
        {
            Directory.Delete(staging, recursive: true);
            throw new InvalidOperationException("便携版压缩包里没有找到 AstralParty.Toys.exe 和 AstralParty.Toys.dll。");
        }
        return staging;
    }

    /// <summary>
    /// 下载指定的安装包，并实时报告下载进度。下载完成后自动核对 SHA-256 哈希值。
    /// </summary>
    public async Task<string> DownloadPackageAsync(
        AppAssetPackageInfo package,
        string? mirrorType = null,
        string? customMirrorUrl = null,
        IProgress<(long received, long total, int? percent)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(package.Url))
        {
            throw new ArgumentException("安装包下载链接为空", nameof(package));
        }

        var downloadUrl = GitHubMirrorHelper.ApplyMirror(package.Url, mirrorType, customMirrorUrl);
        var tempDir = Path.Combine(Path.GetTempPath(), "AstralPartyToysUpdate");
        Directory.CreateDirectory(tempDir);

        var safeFileName = string.IsNullOrWhiteSpace(package.FileName)
            ? Path.GetFileName(new Uri(package.Url).AbsolutePath)
            : package.FileName;
        if (string.IsNullOrWhiteSpace(safeFileName)) safeFileName = "AstralParty.Toys-update.exe";

        var partPath = Path.Combine(tempDir, safeFileName + ".part");
        var finalPath = Path.Combine(tempDir, safeFileName);

        if (File.Exists(partPath)) File.Delete(partPath);

        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        request.Headers.Add("User-Agent", "AstralParty.Toys-Updater");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? package.Size;
        long receivedBytes = 0;

        await using (var remoteStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var localStream = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await remoteStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await localStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                receivedBytes += read;

                int? percent = totalBytes > 0 ? (int)Math.Clamp(receivedBytes * 100 / totalBytes, 0, 100) : null;
                progress?.Report((receivedBytes, totalBytes, percent));
            }
        }

        // SHA-256 完整性安全校验
        if (!string.IsNullOrWhiteSpace(package.Sha256))
        {
            var actualHash = await ComputeSha256Async(partPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(actualHash, package.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(partPath);
                throw new InvalidOperationException($"安装包完整性校验失败: 期望 SHA256={package.Sha256}, 实际计算={actualHash}");
            }
        }

        if (File.Exists(finalPath)) File.Delete(finalPath);
        File.Move(partPath, finalPath);

        return finalPath;
    }

    /// <summary>
    /// 启动安装程序。支持静默安装参数。
    /// </summary>
    public static Process? LaunchInstaller(string installerPath, bool silent = true, bool addDesktopShortcut = false)
    {
        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException("找不到安装程序文件", installerPath);
        }

        var arguments = InstallerArguments(silent, addDesktopShortcut);
        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = arguments,
            UseShellExecute = true
        };

        return Process.Start(psi);
    }

    /// <summary>
    /// 安装程序参数。从单文件版 / 便携版改用安装版时是第一次安装，没有可以沿用的选项，
    /// 而桌面快捷方式默认不勾：不加上的话，用户桌面上什么都没有，会以为没更新。
    /// MERGETASKS 只在默认选项上追加，不会关掉别的。
    /// </summary>
    public static string InstallerArguments(bool silent, bool addDesktopShortcut)
    {
        var args = silent ? "/SILENT /SP- /CLOSEAPPLICATIONS" : "";
        if (addDesktopShortcut) args = (args + " /MERGETASKS=\"desktopicon\"").Trim();
        return args;
    }

    /// <summary>
    /// 计算指定文件的 SHA-256 哈希值（小写十六进制）。
    /// </summary>
    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var bytes = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// 语义化版本比对（候选版本是否严格大于当前版本）。
    /// </summary>
    public static bool IsNewerVersion(string currentVersion, string candidateVersion)
    {
        static int[] Parse(string v)
        {
            var clean = (v ?? "").Trim().TrimStart('v', 'V');
            var segments = clean.Split('.');
            var result = new int[3];
            for (var i = 0; i < Math.Min(segments.Length, 3); i++)
            {
                var s = segments[i];
                var plus = s.IndexOf('+');
                if (plus >= 0) s = s[..plus];
                var dash = s.IndexOf('-');
                if (dash >= 0) s = s[..dash];

                int.TryParse(s, out result[i]);
            }
            return result;
        }

        var cur = Parse(currentVersion);
        var cand = Parse(candidateVersion);

        for (var i = 0; i < 3; i++)
        {
            if (cand[i] > cur[i]) return true;
            if (cand[i] < cur[i]) return false;
        }

        return false;
    }
}
