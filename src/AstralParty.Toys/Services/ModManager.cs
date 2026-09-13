using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;

namespace AstralParty.Toys.Services;

/// <summary>
/// 游戏 Mod 加载器（CesiumLoader version.dll Doorstop 式代理）与 mod 的安装 / 卸载 / 列表管理。
/// 加载器本体 version.dll（Doorstop 式代理，UnityPlayer 导入 version.dll 时被优先加载）与
/// 内置 SDK（CesiumLoader.SDK.dll）、示例 mod（ActivityLogMod.dll）、
/// 各 mod 的 sidecar 元数据（*.json）以及 doorstop_config.json 以「内嵌资源」编译进程序集
/// （AstralParty.Toys.ModLoader.*）。
/// 安装 = 把 version.dll 写为游戏 exe 目录下的 version.dll，并创建
/// AstralParty_ModLoader\{sdk,mods,logs} 目录结构 + 写入 doorstop_config.json；
/// 卸载前校验 DLL 哈希，避免误删他人文件。
/// </summary>
public sealed class ModManager
{
    public const string LoaderDllName = "version.dll";
    public const string LoaderFolderName = "AstralParty_ModLoader";
    public const string ModsFolderName = "mods";
    public const string SdkFolderName = "sdk";
    public const string LogsFolderName = "logs";
    public const string ConfigFileName = "doorstop_config.json";
    public const string SdkDllName = "CesiumLoader.SDK.dll";
    public const string SampleModDllName = "ActivityLogMod.dll";

    private const string ResourcePrefix = "AstralParty.Toys.ModLoader.";

    /// <summary>内嵌加载器版本号；来源：构建时下载的 cesium-loader.json 里 version 字段（CI 自动更新），本地开发构建为 dev-local。</summary>
    private static readonly string EmbeddedVersion = ReadEmbeddedVersion();

    private static string ReadEmbeddedVersion()
    {
        try
        {
            using var stream = typeof(ModManager).Assembly.GetManifestResourceStream(ResourcePrefix + "loader-version.json");
            if (stream is null) return "";
            using var reader = new StreamReader(stream);
            var raw = reader.ReadToEnd().Trim().Trim('"');
            return string.IsNullOrWhiteSpace(raw) || raw == "unknown" ? "" : raw;
        }
        catch
        {
            return "";
        }
    }

    private static readonly string[] GameExeNames = ["astralparty.exe", "astralparty_cn.exe"];

    private static readonly Assembly Assembly = typeof(ModManager).Assembly;

    private static readonly byte[]? EmbeddedLoaderDll = ReadEmbeddedResource(LoaderDllName);
    private static readonly byte[]? EmbeddedConfig = ReadEmbeddedResource(ConfigFileName);
    private static readonly byte[]? EmbeddedSdkDll = ReadEmbeddedResource(SdkDllName);
    private static readonly byte[]? EmbeddedSampleModDll = ReadEmbeddedResource(SampleModDllName);
    private static readonly byte[]? EmbeddedSampleSidecar = ReadEmbeddedResource(SampleModDllName.Replace(".dll", ".json"));
    private static readonly string? EmbeddedLoaderHash = EmbeddedLoaderDll is null
        ? null
        : Convert.ToHexString(SHA256.HashData(EmbeddedLoaderDll));

    private static byte[]? ReadEmbeddedResource(string name)
    {
        using var stream = Assembly.GetManifestResourceStream(ResourcePrefix + name);
        if (stream is null) return null;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private readonly string _profileDirectory;

    /// <param name="appDirectory">工具目录；只用于提示信息。</param>
    /// <param name="profileDirectory">配置根目录（游戏目录记忆等），显式指定供测试。</param>
    public ModManager(string appDirectory, string? profileDirectory = null)
    {
        _ = appDirectory;
        _profileDirectory = profileDirectory ?? SpeedhackManager.ResolveProfileDirectory();
    }

    public static bool HasEmbeddedLoader => EmbeddedLoaderDll is not null;
    public static bool HasEmbeddedConfig => EmbeddedConfig is not null;
    public static bool HasEmbeddedSdk => EmbeddedSdkDll is not null;
    public static bool HasEmbeddedSampleMod => EmbeddedSampleModDll is not null;

    private string StateFilePath => Path.Combine(_profileDirectory, "modloader-state.json");

    // ============================== 状态 ==============================

    public ModStatus GetStatus()
    {
        var status = new ModStatus();
        try
        {
            status.BundleLoaderPresent = HasEmbeddedLoader;
            status.BundleConfigPresent = HasEmbeddedConfig;
            status.BundleSdkPresent = HasEmbeddedSdk;
            status.BundleSampleModPresent = HasEmbeddedSampleMod;

            var directory = GetStoredGameDirectory();
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                directory = SpeedhackManager.DetectGameDirectory() ?? "";
                if (!string.IsNullOrWhiteSpace(directory)) status.AutoDetectedDirectory = directory;
            }
            status.GameDirectory = directory;
            status.GameExeFound = !string.IsNullOrEmpty(directory) && SpeedhackManager.ContainsGameExe(directory) is not null;

            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                var dllPath = Path.Combine(directory, LoaderDllName);
                status.LoaderPath = dllPath;
                status.LoaderPresent = File.Exists(dllPath);
                if (status.LoaderPresent)
                {
                    status.LoaderMatchesBundle = MatchesEmbeddedLoader(dllPath);
                    status.Installed = true;
                }
            }

            status.GameRunning = SpeedhackManager.IsGameRunning();

            // 已安装版本：游戏目录 AstralParty_ModLoader\cesium-loader.json（安装时写入）
            var loaderRootForVersion = string.IsNullOrEmpty(directory) ? "" : Path.Combine(directory, LoaderFolderName);
            status.InstalledVersion = loaderRootForVersion.Length > 0
                ? ReadInstalledVersion(Path.Combine(loaderRootForVersion, "cesium-loader.json"))
                : "";
            status.EmbeddedVersion = EmbeddedVersion;

            // mod 列表 / SDK 列表（安装目录存在才扫）
            var loaderRoot = string.IsNullOrEmpty(directory) ? "" : Path.Combine(directory, LoaderFolderName);
            status.LoaderRoot = loaderRoot;
            if (loaderRoot.Length > 0)
            {
                status.LoaderRootExists = Directory.Exists(loaderRoot);
                status.Mods = ScanMods(Path.Combine(loaderRoot, ModsFolderName));
                status.Sdk = ScanMods(Path.Combine(loaderRoot, SdkFolderName));
            }

            status.BundleHash = EmbeddedLoaderHash ?? "";
            status.Message = BuildStatusMessage(status);
        }
        catch (Exception ex)
        {
            status.Message = $"读取状态失败：{ex.Message}";
        }
        return status;
    }

    private string BuildStatusMessage(ModStatus status)
    {
        if (!HasEmbeddedLoader)
            return "程序集缺少内置加载器资源（ModLoader.version.dll），请重新编译发布版本。";
        if (string.IsNullOrEmpty(status.GameDirectory))
            return "尚未找到游戏目录：可点击「选择游戏目录」手动指定安装位置。";
        if (!Directory.Exists(status.GameDirectory))
            return $"游戏目录不存在：{status.GameDirectory}";
        if (status.GameRunning)
            return "检测到游戏正在运行——安装或卸载前请先退出游戏。";
        if (status.Installed)
            return status.LoaderMatchesBundle
                ? $"已安装（{status.GameDirectory}），mod 目录 {ModsFolderName}\\ 中有 {status.Mods.Count} 个 mod。"
                : "游戏目录存在其它 version.dll（与内置文件不同）——覆盖或卸载前请先确认来源。";
        return "尚未安装：点击「安装加载器」把文件复制到游戏目录。";
    }

    private static List<ModEntryInfo> ScanMods(string directory)
    {
        var list = new List<ModEntryInfo>();
        try
        {
            if (!Directory.Exists(directory)) return list;
            foreach (var file in Directory.EnumerateFiles(directory, "*.dll").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var info = new FileInfo(file);
                var entry = new ModEntryInfo
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    FileName = Path.GetFileName(file),
                    SizeBytes = info.Length,
                    ModifiedUtc = info.LastWriteTimeUtc
                };
                // 同名 sidecar(由 SDK SdkManifest.ExportSidecar / 脚手架 cesium new 写出):
                // {id,name,version,author,description,permissions,sdkVersion,dependencies}
                var sidecar = Path.ChangeExtension(file, ".json");
                if (File.Exists(sidecar))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(sidecar));
                        var root = doc.RootElement;
                        entry.Id = root.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                        entry.DisplayName = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        entry.Version = root.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
                        entry.Author = root.TryGetProperty("author", out var a) ? a.GetString() ?? "" : "";
                        entry.Description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                        entry.SdkVersion = root.TryGetProperty("sdkVersion", out var s) ? s.GetString() ?? "" : "";
                        entry.Enabled = !root.TryGetProperty("enabled", out var en) || en.ValueKind != JsonValueKind.False;
                        entry.Permissions = root.TryGetProperty("permissions", out var p) && p.ValueKind == JsonValueKind.Number
                            ? p.GetInt32() : 0;
                        if (root.TryGetProperty("dependencies", out var deps) && deps.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var dep in deps.EnumerateArray())
                            {
                                var id = dep.TryGetProperty("id", out var dId) ? dId.GetString() ?? "" : "";
                                var minV = dep.TryGetProperty("minVersion", out var dMin) ? dMin.GetString() ?? "" : "";
                                if (!string.IsNullOrEmpty(id))
                                    entry.Dependencies.Add(new ModDependencyInfo { Id = id, MinVersion = minV });
                            }
                        }
                    }
                    catch
                    {
                        // sidecar 损坏按无 sidecar 处理
                    }
                }
                list.Add(entry);
            }
        }
        catch
        {
            // 扫描失败按空列表处理
        }
        return list;
    }

    // ============================== 安装 / 卸载 ==============================

    public static string DescribeRunningInstall() =>
        "游戏正在运行时也能装：version.dll 由游戏启动时加载，本次写进去要重启游戏才会生效；" +
        "而且正在运行的游戏往往占用着 version.dll，覆盖会失败并提示文件被占用——那样就先完全退出游戏再装。";

    public static string DescribeRunningUninstall() =>
        "游戏正在运行时也能卸：version.dll 已被游戏加载占用，删除通常会失败并提示文件被占用；" +
        "即使删除成功，当前这局也已经加载了加载器，要重启游戏才会恢复正常。";

    public static string DescribeInstalled() => SpeedhackManager.IsGameRunning()
        ? "加载器文件已写入游戏目录。游戏正在运行，本次不会立刻生效——重启游戏后才会加载 mod。"
        : "加载器已安装到游戏目录。启动游戏后会自动加载 mods\\ 目录下的所有 mod，并在控制台窗口显示日志。";

    /// <summary>把内嵌文件写入游戏目录 + 创建目录结构。version.dll 目标已存在且不是内置文件时，必须 overwriteDll 才会覆盖。</summary>
    public void Install(string gameDirectory, bool overwriteDll, bool includeSampleMod)
    {
        if (!HasEmbeddedLoader)
            throw new InvalidOperationException("程序集缺少内置加载器资源（version.dll），无法安装。");
        if (string.IsNullOrWhiteSpace(gameDirectory))
            throw new ArgumentException("请先选择游戏目录。");
        if (!Directory.Exists(gameDirectory))
            throw new DirectoryNotFoundException($"游戏目录不存在：{gameDirectory}");
        if (string.IsNullOrEmpty(SpeedhackManager.ContainsGameExe(gameDirectory)))
            throw new InvalidOperationException("所选目录里没有找到 AstralParty.exe / AstralParty_CN.exe，确认这是游戏 exe 所在的目录？");

        var targetDll = Path.Combine(gameDirectory, LoaderDllName);
        if (File.Exists(targetDll) && !MatchesEmbeddedLoader(targetDll) && !overwriteDll)
            throw new InvalidOperationException(
                "游戏目录已存在一个与内置不同的 version.dll（可能是其它工具的，如旧版独立变速器）——如确定要覆盖，请勾选「允许覆盖其它 version.dll」。");

        // 互斥检测: 已安装旧版独立变速器(speedhack-rs)? 两者都写 version.dll, 会互相覆盖。
        if (HasSpeedhackInstalled(gameDirectory))
            throw new InvalidOperationException(
                "检测到已安装独立变速器（游戏目录存在 speedhack_config.json）。" +
                "加载器与变速器共用 version.dll，不能同时安装：装加载器会覆盖变速器，变速热键配置将失效。" +
                "提示：CesiumLoader 已内置变速引擎——装加载器后改 AstralParty_ModLoader\\doorstop_config.json " +
                "里的 speedhackBaseSpeed 即可变速（无需独立变速器）。");

        try
        {
            WriteAllBytesProtected(targetDll, EmbeddedLoaderDll!);

            // 目录结构
            var loaderRoot = Path.Combine(gameDirectory, LoaderFolderName);
            Directory.CreateDirectory(Path.Combine(loaderRoot, ModsFolderName));
            Directory.CreateDirectory(Path.Combine(loaderRoot, SdkFolderName));
            Directory.CreateDirectory(Path.Combine(loaderRoot, LogsFolderName));

            // doorstop_config.json（加载器配置：enabled / 变速基础倍率 / SDK 版本等）
            // 已存在则保留用户配置(不覆盖, 避免重置变速/开关设置); 不存在才写模板。
            var configPath = Path.Combine(loaderRoot, ConfigFileName);
            if (!File.Exists(configPath) && HasEmbeddedConfig)
                WriteAllBytesProtected(configPath, EmbeddedConfig!);

            // 内置 SDK
            if (HasEmbeddedSdk)
                WriteAllBytesProtected(Path.Combine(loaderRoot, SdkFolderName, SdkDllName), EmbeddedSdkDll!);

            // 示例 mod（可选）：DLL + sidecar 元数据（依赖解析/权限/版本协商需要 sidecar）
            if (includeSampleMod && HasEmbeddedSampleMod)
            {
                WriteAllBytesProtected(Path.Combine(loaderRoot, ModsFolderName, SampleModDllName), EmbeddedSampleModDll!);
                if (EmbeddedSampleSidecar is not null)
                    WriteAllBytesProtected(Path.Combine(loaderRoot, ModsFolderName, SampleModDllName.Replace(".dll", ".json")), EmbeddedSampleSidecar);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                $"写入游戏目录失败：{ex.Message}。" +
                (SpeedhackManager.IsGameRunning() ? "游戏正在运行时 version.dll 会被占用，无法覆盖——请完全退出游戏后再安装。" : ""), ex);
        }

        SaveStoredGameDirectory(gameDirectory);
    }

    /// <summary>从游戏目录删除 version.dll 与 AstralParty_ModLoader 目录。DLL 与内置不一致时默认拒绝，force 才删除。</summary>
    public UninstallResult Uninstall(string gameDirectory, bool force)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
            throw new DirectoryNotFoundException($"游戏目录不存在：{gameDirectory ?? "(未选择)"}");

        var dllPath = Path.Combine(gameDirectory, LoaderDllName);
        var loaderRoot = Path.Combine(gameDirectory, LoaderFolderName);
        var dllExists = File.Exists(dllPath);
        var loaderRootExists = Directory.Exists(loaderRoot);

        if (!dllExists && !loaderRootExists)
            return new UninstallResult { Message = "游戏目录中没有加载器文件，无需卸载。" };

        if (dllExists && !MatchesEmbeddedLoader(dllPath) && !force)
            throw new InvalidOperationException(
                "该 version.dll 与内置文件不同，可能不是本工具安装的——未删除任何文件。如确认要删除请勾选「强制卸载」。");

        var result = new UninstallResult();
        if (dllExists)
        {
            try
            {
                File.Delete(dllPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new IOException(
                    $"删除 version.dll 失败：{ex.Message}。" +
                    (SpeedhackManager.IsGameRunning()
                        ? "游戏正在运行时该文件已被加载占用，请完全退出游戏后再卸载（本次没有改动任何文件）。"
                        : "请检查文件权限后重试（本次没有改动任何文件）。"), ex);
            }
            result.RemovedDll = true;
        }
        if (loaderRootExists)
        {
            try
            {
                Directory.Delete(loaderRoot, recursive: true);
                result.RemovedConfig = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.Message = $"version.dll 已删除，但 {LoaderFolderName} 目录删除失败：{ex.Message}（可能仍有文件被占用）。";
                return result;
            }
        }

        result.Message = SpeedhackManager.IsGameRunning()
            ? "加载器已卸载；当前正在运行的游戏进程仍加载着加载器，重启游戏后恢复正常。"
            : "加载器已卸载（游戏恢复正常）。";
        return result;
    }

    // ============================== 联网更新 ==============================

    /// <summary>CesiumLoader 最新发布包的固定下载地址（GitHub Release latest）。</summary>
    public const string LatestPackageUrl =
        "https://github.com/higashitaniyume/CesiumLoader/releases/latest/download/cesium-loader.zip";

    public const string InstalledManifestFileName = "cesium-loader.json";

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AstralParty.Toys/" + typeof(ModManager).Assembly.GetName().Version);
        return client;
    }

    private static string ReadInstalledVersion(string manifestPath)
    {
        try
        {
            if (!File.Exists(manifestPath)) return "";
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            return document.RootElement.TryGetProperty("version", out var element) ? element.GetString() ?? "" : "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>下载最新发布包到字节数组（不落盘）。网络异常/无 Release 时抛异常。</summary>
    public async Task<byte[]> DownloadLatestPackageAsync(CancellationToken cancellationToken = default)
    {
        using var response = await Http.GetAsync(LatestPackageUrl, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            throw new InvalidOperationException(
                status == 404
                    ? "CesiumLoader 还没有发布过任何 Release（打 modloader-* tag 触发 GitHub Action 即可）。"
                    : $"下载失败：HTTP {(int)response.StatusCode}。");
        }
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>解析发布包清单（zip 内的 cesium-loader.json）：版本 + 各文件 SHA256。</summary>
    public static LoaderPackageInfo? ParsePackageManifest(byte[] zipBytes)
    {
        try
        {
            using var stream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var entry = archive.GetEntry(InstalledManifestFileName);
            if (entry is null) return null;
            using var reader = new StreamReader(entry.Open());
            var json = reader.ReadToEnd();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var info = new LoaderPackageInfo { Version = root.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "" };
            if (root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in files.EnumerateObject())
                {
                    info.Files[property.Name] = property.Value.GetString() ?? "";
                }
            }
            return info;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>校验发布包内容：文件齐全 + SHA256 与清单一致（未发布清单时仅校验存在）。</summary>
    private static void ValidatePackage(byte[] zipBytes, LoaderPackageInfo manifest)
    {
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var requiredRelative = new[]
        {
            "version.dll",
            "AstralParty_ModLoader/doorstop_config.json",
            "AstralParty_ModLoader/sdk/CesiumLoader.SDK.dll",
            "AstralParty_ModLoader/mods/ActivityLogMod.dll",
            "AstralParty_ModLoader/mods/ActivityLogMod.json"
        };

        foreach (var relative in requiredRelative)
        {
            var entry = archive.GetEntry(relative);
            if (entry is null) throw new InvalidDataException($"发布包缺少文件：{relative}");
            if (manifest.Files.TryGetValue(relative, out var expected) && expected.Length > 0)
            {
                using var entryStream = entry.Open();
                using var sha = SHA256.Create();
                var actual = Convert.ToHexString(sha.ComputeHash(entryStream)).ToLowerInvariant();
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"发布包校验失败：{relative} 哈希不一致。");
            }
        }
    }

    /// <summary>把发布包内容写入游戏目录（version.dll + doorstop_config + sdk + mods + sidecar + 清单）。游戏运行中覆盖 version.dll 会因占用失败。</summary>
    public void InstallPackage(string gameDirectory, byte[] zipBytes, bool overwriteDll)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory))
            throw new ArgumentException("请先选择游戏目录。");
        if (!Directory.Exists(gameDirectory))
            throw new DirectoryNotFoundException($"游戏目录不存在：{gameDirectory}");
        if (string.IsNullOrEmpty(SpeedhackManager.ContainsGameExe(gameDirectory)))
            throw new InvalidOperationException("所选目录里没有找到 AstralParty.exe / AstralParty_CN.exe，确认这是游戏 exe 所在的目录？");

        var manifest = ParsePackageManifest(zipBytes)
            ?? throw new InvalidDataException("发布包缺少清单（cesium-loader.json），已停止安装。");
        ValidatePackage(zipBytes, manifest);

        var targetDll = Path.Combine(gameDirectory, LoaderDllName);
        if (File.Exists(targetDll) && !MatchesEmbeddedLoader(targetDll) && !overwriteDll)
            throw new InvalidOperationException(
                "游戏目录已存在一个与内置不同的 version.dll（可能是其它工具的，如旧版独立变速器）——如确定要覆盖，请勾选「允许覆盖其它 version.dll」。");

        // 互斥检测: 已安装旧版独立变速器
        if (HasSpeedhackInstalled(gameDirectory))
            throw new InvalidOperationException(
                "检测到已安装独立变速器（游戏目录存在 speedhack_config.json）。" +
                "加载器与变速器共用 version.dll，不能同时安装：装加载器会覆盖变速器，变速热键配置将失效。" +
                "提示：CesiumLoader 已内置变速引擎——装加载器后改 AstralParty_ModLoader\\doorstop_config.json " +
                "里的 speedhackBaseSpeed 即可变速（无需独立变速器）。");

        try
        {
            using var stream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var loaderRoot = Path.Combine(gameDirectory, LoaderFolderName);
            Directory.CreateDirectory(Path.Combine(loaderRoot, ModsFolderName));
            Directory.CreateDirectory(Path.Combine(loaderRoot, SdkFolderName));
            Directory.CreateDirectory(Path.Combine(loaderRoot, LogsFolderName));

            // version.dll → 游戏 exe 目录（Doorstop 代理）
            ExtractEntryToFile(archive, "version.dll", targetDll);
            // doorstop_config.json → AstralParty_ModLoader\
            // 已存在则保留用户配置(变速/开关等), 不覆盖; 不存在才从包提取。
            var configPath = Path.Combine(loaderRoot, ConfigFileName);
            if (!File.Exists(configPath))
                ExtractEntryToFile(archive, "AstralParty_ModLoader/doorstop_config.json", configPath);
            // SDK
            ExtractEntryToFile(archive, "AstralParty_ModLoader/sdk/CesiumLoader.SDK.dll",
                Path.Combine(loaderRoot, SdkFolderName, SdkDllName));
            // 示例 mod: DLL + sidecar
            ExtractEntryToFile(archive, "AstralParty_ModLoader/mods/ActivityLogMod.dll",
                Path.Combine(loaderRoot, ModsFolderName, SampleModDllName));
            ExtractEntryToFile(archive, "AstralParty_ModLoader/mods/ActivityLogMod.json",
                Path.Combine(loaderRoot, ModsFolderName, SampleModDllName.Replace(".dll", ".json")));

            // 写入安装清单（记录版本，供 GetStatus 显示/对比）
            var manifestEntry = archive.GetEntry(InstalledManifestFileName);
            if (manifestEntry is not null)
            {
                using var reader = new StreamReader(manifestEntry.Open());
                File.WriteAllText(Path.Combine(loaderRoot, InstalledManifestFileName), reader.ReadToEnd());
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                $"写入游戏目录失败：{ex.Message}。" +
                (SpeedhackManager.IsGameRunning() ? "游戏正在运行时 version.dll 会被占用，无法覆盖——请完全退出游戏后再更新。" : ""), ex);
        }

        SaveStoredGameDirectory(gameDirectory);
    }

    private static void ExtractEntryToFile(ZipArchive archive, string entryName, string targetPath)
    {
        var entry = archive.GetEntry(entryName)
            ?? throw new InvalidDataException($"发布包缺少文件：{entryName}");
        WriteAllBytesProtected(targetPath, ReadEntryBytes(entry));
    }

    private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    // ============================== 游戏变速（加载器内置功能） ==============================

    /// <summary>设置加载器内置变速的基础倍率: 写 doorstop_config.json 的 speedhackBaseSpeed。
    /// 1.0 = 正常(禁用变速); 其他值(如 2.0) = 启动游戏即变速, 全程保持。重启游戏生效。</summary>
    public void SetSpeedhack(string gameDirectory, double baseSpeed)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory))
            throw new ArgumentException("请先选择游戏目录。");
        if (baseSpeed <= 0 || baseSpeed > 100)
            throw new InvalidOperationException("倍速必须在 (0,100] 之间（1.0 = 正常速度）。");

        var config = ReadLoaderConfig(gameDirectory);
        config.SpeedhackBaseSpeed = baseSpeed;
        SaveLoaderConfig(gameDirectory, config);
    }

    // ============================== mod 管理 ==============================

    /// <summary>互斥检测: 游戏目录是否已安装旧版独立变速器(以 speedhack_config.json 为标志)。</summary>
    public static bool HasSpeedhackInstalled(string gameDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory)) return false;
            return File.Exists(Path.Combine(gameDirectory, SpeedhackManager.ConfigName));
        }
        catch
        {
            return false;
        }
    }

    // ============================== 加载器配置 (doorstop_config.json) ==============================

    /// <summary>加载器配置完整路径。</summary>
    public string LoaderConfigPath(string gameDirectory)
        => Path.Combine(gameDirectory, LoaderFolderName, ConfigFileName);

    /// <summary>读取加载器配置; 文件缺失时用内置模板的默认值。</summary>
    public LoaderConfig ReadLoaderConfig(string gameDirectory)
    {
        var path = LoaderConfigPath(gameDirectory);
        var defaults = new LoaderConfig();
        try
        {
            if (!File.Exists(path)) return defaults;
            var json = StripJsonComments(File.ReadAllText(path));
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("enabled", out var e) && e.ValueKind == JsonValueKind.True) defaults.Enabled = true;
            if (root.TryGetProperty("enabled", out e) && e.ValueKind == JsonValueKind.False) defaults.Enabled = false;
            if (root.TryGetProperty("useManagedBootstrap", out var m) && m.ValueKind == JsonValueKind.True) defaults.UseManagedBootstrap = true;
            if (root.TryGetProperty("useManagedBootstrap", out m) && m.ValueKind == JsonValueKind.False) defaults.UseManagedBootstrap = false;
            if (root.TryGetProperty("bootstrapAssembly", out var ba) && ba.ValueKind == JsonValueKind.String) defaults.BootstrapAssembly = ba.GetString()!;
            if (root.TryGetProperty("bootstrapType", out var bt) && bt.ValueKind == JsonValueKind.String) defaults.BootstrapType = bt.GetString()!;
            if (root.TryGetProperty("bootstrapMethod", out var bm) && bm.ValueKind == JsonValueKind.String) defaults.BootstrapMethod = bm.GetString()!;
            if (root.TryGetProperty("gameAssemblyTimeoutSec", out var gt) && gt.ValueKind == JsonValueKind.Number) defaults.GameAssemblyTimeoutSec = gt.GetInt32();
            if (root.TryGetProperty("domainTimeoutSec", out var dt) && dt.ValueKind == JsonValueKind.Number) defaults.DomainTimeoutSec = dt.GetInt32();
            if (root.TryGetProperty("hybridclrTimeoutSec", out var ht) && ht.ValueKind == JsonValueKind.Number) defaults.HybridclrTimeoutSec = ht.GetInt32();
            if (root.TryGetProperty("consoleEnabled", out var ce) && ce.ValueKind == JsonValueKind.True) defaults.ConsoleEnabled = true;
            if (root.TryGetProperty("consoleEnabled", out ce) && ce.ValueKind == JsonValueKind.False) defaults.ConsoleEnabled = false;
            if (root.TryGetProperty("consoleTopmost", out var ct) && ct.ValueKind == JsonValueKind.True) defaults.ConsoleTopmost = true;
            if (root.TryGetProperty("consoleTopmost", out ct) && ct.ValueKind == JsonValueKind.False) defaults.ConsoleTopmost = false;
            if (root.TryGetProperty("forwardActivityLog", out var fa) && fa.ValueKind == JsonValueKind.True) defaults.ForwardActivityLog = true;
            if (root.TryGetProperty("forwardActivityLog", out fa) && fa.ValueKind == JsonValueKind.False) defaults.ForwardActivityLog = false;
            if (root.TryGetProperty("speedhackBaseSpeed", out var sp) && sp.ValueKind == JsonValueKind.Number) defaults.SpeedhackBaseSpeed = sp.GetDouble();
            if (root.TryGetProperty("sdkVersion", out var sv) && sv.ValueKind == JsonValueKind.String) defaults.SdkVersion = sv.GetString()!;
        }
        catch
        {
            // 损坏配置按默认值返回(加载器同样容错)
        }
        return defaults;
    }

    /// <summary>保存加载器配置(写回 doorstop_config.json, 无注释)。speedhackBaseSpeed 限制在 (0,100]。</summary>
    public void SaveLoaderConfig(string gameDirectory, LoaderConfig config)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory))
            throw new ArgumentException("请先选择游戏目录。");
        if (config.SpeedhackBaseSpeed <= 0 || config.SpeedhackBaseSpeed > 100)
            throw new InvalidOperationException("变速基础倍率必须在 (0,100] 之间（1.0 = 正常速度）。");

        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["enabled"] = config.Enabled,
            ["useManagedBootstrap"] = config.UseManagedBootstrap,
            ["bootstrapAssembly"] = config.BootstrapAssembly,
            ["bootstrapType"] = config.BootstrapType,
            ["bootstrapMethod"] = config.BootstrapMethod,
            ["gameAssemblyTimeoutSec"] = config.GameAssemblyTimeoutSec,
            ["domainTimeoutSec"] = config.DomainTimeoutSec,
            ["hybridclrTimeoutSec"] = config.HybridclrTimeoutSec,
            ["consoleEnabled"] = config.ConsoleEnabled,
            ["consoleTopmost"] = config.ConsoleTopmost,
            ["forwardActivityLog"] = config.ForwardActivityLog,
            ["speedhackBaseSpeed"] = config.SpeedhackBaseSpeed,
            ["sdkVersion"] = config.SdkVersion
        }, new JsonSerializerOptions { WriteIndented = true });
        WriteAllBytesProtected(LoaderConfigPath(gameDirectory), System.Text.Encoding.UTF8.GetBytes(json));
    }

    // ============================== mod 配置表单 (configs\{mod}.json 键值编辑) ==============================

    /// <summary>读取 mod 配置为表单字段列表(按 JSON 值类型分类); 文件不存在返回空列表。
    /// 每个字段带 name + kind(bool/number/string) + 当前值。</summary>
    public List<ModConfigField> ReadModConfigFields(string gameDirectory, string fileName)
    {
        var list = new List<ModConfigField>();
        var text = ReadModConfig(gameDirectory, fileName);
        if (string.IsNullOrWhiteSpace(text)) return list;
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return list;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var field = new ModConfigField { Name = prop.Name };
                switch (prop.Value.ValueKind)
                {
                    case JsonValueKind.True:
                        field.Kind = "bool"; field.BoolValue = true; break;
                    case JsonValueKind.False:
                        field.Kind = "bool"; field.BoolValue = false; break;
                    case JsonValueKind.Number:
                        field.Kind = "number";
                        field.NumberValue = prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble();
                        break;
                    case JsonValueKind.String:
                        field.Kind = "string"; field.StringValue = prop.Value.GetString() ?? ""; break;
                    default:
                        field.Kind = "other"; field.StringValue = prop.Value.GetRawText(); break;
                }
                list.Add(field);
            }
        }
        catch
        {
            // 损坏配置按空处理(用户可重新编辑)
        }
        return list;
    }

    /// <summary>按表单字段保存 mod 配置(序列化回 JSON; 类型由字段 Kind 决定)。</summary>
    public void SaveModConfigFields(string gameDirectory, string fileName, List<ModConfigField> fields)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("游戏目录或 mod 文件名无效。");
        var loaderRoot = Path.Combine(gameDirectory, LoaderFolderName);
        var configsDir = Path.Combine(loaderRoot, "configs");
        Directory.CreateDirectory(configsDir);
        var path = Path.Combine(configsDir, Path.GetFileNameWithoutExtension(fileName) + ".json");

        var dict = new Dictionary<string, object?>();
        foreach (var field in fields)
        {
            switch (field.Kind)
            {
                case "bool": dict[field.Name] = field.BoolValue; break;
                case "number": dict[field.Name] = field.NumberValue; break;
                default: dict[field.Name] = field.StringValue; break;
            }
        }
        var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
        WriteAllBytesProtected(path, System.Text.Encoding.UTF8.GetBytes(json));
    }

    /// <summary>剥掉 JSON 里的 // 与 /* */ 注释(加载器配置文件带注释, System.Text.Json 不认)。</summary>
    private static string StripJsonComments(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;
        var sb = new System.Text.StringBuilder(json.Length);
        var inString = false;
        var i = 0;
        while (i < json.Length)
        {
            var c = json[i];
            if (inString)
            {
                sb.Append(c);
                if (c == '\\' && i + 1 < json.Length) { sb.Append(json[i + 1]); i += 2; continue; }
                if (c == '"') inString = false;
                i++;
                continue;
            }
            if (c == '"') { inString = true; sb.Append(c); i++; continue; }
            if (c == '/' && i + 1 < json.Length && json[i + 1] == '/')
            {
                while (i < json.Length && json[i] != '\n') i++;
                continue;
            }
            if (c == '/' && i + 1 < json.Length && json[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < json.Length && !(json[i] == '*' && json[i + 1] == '/')) i++;
                i += 2;
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>切换 mod 启用/禁用: 改写 sidecar(mods\{name}.json) 的 enabled 字段。
    /// 加载器读取 sidecar 的 enabled=false 时跳过该 mod。无 sidecar 的 mod 自动补一个。</summary>
    public bool ToggleMod(string gameDirectory, string fileName, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("游戏目录或 mod 文件名无效。");
        var modsDir = Path.Combine(gameDirectory, LoaderFolderName, ModsFolderName);
        var dllPath = Path.Combine(modsDir, fileName);
        if (!File.Exists(dllPath)) throw new FileNotFoundException($"mod 不存在：{fileName}");

        var sidecarPath = Path.Combine(modsDir, Path.GetFileNameWithoutExtension(fileName) + ".json");
        string json;
        if (File.Exists(sidecarPath))
        {
            json = File.ReadAllText(sidecarPath);
        }
        else
        {
            // 无 sidecar: 生成最小结构(id 用程序集名, 保持加载器兼容)
            json = "{\"id\":\"" + Path.GetFileNameWithoutExtension(fileName) + "\",\"name\":\"" +
                   Path.GetFileNameWithoutExtension(fileName) + "\",\"version\":\"0.0.0\"}";
        }

        var updated = SetJsonBool(json, "enabled", enabled);
        WriteAllBytesProtected(sidecarPath, System.Text.Encoding.UTF8.GetBytes(updated));
        return enabled;
    }

    /// <summary>mod 配置文件路径(configs\{modName}.json, SdkConfig 约定; 不存在时返回 null)。</summary>
    public string? ModConfigPath(string gameDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName)) return null;
        var loaderRoot = Path.Combine(gameDirectory, LoaderFolderName);
        var configsDir = Path.Combine(loaderRoot, "configs");
        var path = Path.Combine(configsDir, Path.GetFileNameWithoutExtension(fileName) + ".json");
        return File.Exists(path) ? path : null;
    }

    /// <summary>读取 mod 配置内容(configs\{modName}.json); 不存在返回空串。</summary>
    public string ReadModConfig(string gameDirectory, string fileName)
    {
        var path = ModConfigPath(gameDirectory, fileName);
        if (path is null) return "";
        try { return File.ReadAllText(path); } catch { return ""; }
    }

    /// <summary>用系统默认编辑器打开 mod 配置文件; 不存在则创建空的。</summary>
    public string OpenModConfig(string gameDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("游戏目录或 mod 文件名无效。");
        var loaderRoot = Path.Combine(gameDirectory, LoaderFolderName);
        var configsDir = Path.Combine(loaderRoot, "configs");
        Directory.CreateDirectory(configsDir);
        var path = Path.Combine(configsDir, Path.GetFileNameWithoutExtension(fileName) + ".json");
        if (!File.Exists(path))
            File.WriteAllText(path, "{\n  // 配置说明见对应 mod 文档; 公开字段(public field)才会被读取\n}\n");
        OpenInExplorer(path);
        return path;
    }

    /// <summary>在 JSON 文本里设置一个布尔字段(保留其它字段与格式; 无该字段则追加)。极简实现, 不做完整 JSON 解析。</summary>
    private static string SetJsonBool(string json, string key, bool value)
    {
        var needle = "\"" + key + "\"";
        var colon = json.IndexOf(needle, StringComparison.Ordinal);
        if (colon >= 0)
        {
            // 找到字段, 替换其值(直到 , 或 })
            var valueStart = json.IndexOf(':', colon) + 1;
            var i = valueStart;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t' || json[i] == '\r' || json[i] == '\n')) i++;
            var j = i;
            while (j < json.Length && json[j] != ',' && json[j] != '}') j++;
            return json[..i] + (value ? "true" : "false") + json[j..];
        }
        // 追加: 插到最后一个 } 前
        var insert = json.LastIndexOf('}');
        if (insert < 0) return json;
        var suffix = json[insert..];
        var prefix = json[..insert].TrimEnd();
        var separator = prefix.EndsWith("{", StringComparison.Ordinal) ? "" : ",";
        return prefix + separator + "\n  \"" + key + "\": " + (value ? "true" : "false") + "\n" + suffix;
    }

    /// <summary>把 mod DLL 复制进游戏 mods 目录。</summary>
    public ModEntryInfo ImportMod(string gameDirectory, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
            throw new DirectoryNotFoundException("游戏目录不存在，请先安装加载器或选择游戏目录。");
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("找不到要导入的 mod 文件。");
        if (!sourcePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("mod 必须是 .dll 文件。");

        var modsDir = Path.Combine(gameDirectory, LoaderFolderName, ModsFolderName);
        Directory.CreateDirectory(modsDir);

        var fileName = Path.GetFileName(sourcePath);
        var target = Path.Combine(modsDir, fileName);
        WriteAllBytesProtected(target, File.ReadAllBytes(sourcePath));

        return new ModEntryInfo
        {
            Name = Path.GetFileNameWithoutExtension(fileName),
            FileName = fileName,
            SizeBytes = new FileInfo(target).Length,
            ModifiedUtc = File.GetLastWriteTimeUtc(target)
        };
    }

    /// <summary>从游戏 mods 目录删除一个 mod DLL。</summary>
    public ModEntryInfo? DeleteMod(string gameDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName))
            return null;
        var target = Path.Combine(gameDirectory, LoaderFolderName, ModsFolderName, fileName);
        if (!File.Exists(target)) return null;

        var info = new ModEntryInfo
        {
            Name = Path.GetFileNameWithoutExtension(fileName),
            FileName = fileName,
            SizeBytes = new FileInfo(target).Length,
            ModifiedUtc = File.GetLastWriteTimeUtc(target)
        };
        try
        {
            File.Delete(target);
            return info;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"删除 {fileName} 失败：{ex.Message}（游戏运行时 DLL 可能被占用）。", ex);
        }
    }

    // ============================== 游戏目录 ==============================

    public string? ResolveGameDirectory() => GetStoredGameDirectory() ?? SpeedhackManager.DetectGameDirectory();

    public string? GetStoredGameDirectory()
    {
        try
        {
            if (!File.Exists(StateFilePath)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(StateFilePath));
            var value = document.RootElement.TryGetProperty("gameDirectory", out var element)
                ? element.GetString() : null;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    public void SaveStoredGameDirectory(string? directory)
    {
        Directory.CreateDirectory(_profileDirectory);
        File.WriteAllText(StateFilePath, JsonSerializer.Serialize(new { gameDirectory = directory }));
    }

    // ============================== 工具 ==============================

    private static bool MatchesEmbeddedLoader(string path)
    {
        if (EmbeddedLoaderHash is null) return false;
        try
        {
            using var stream = File.OpenRead(path);
            return CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(stream), Convert.FromHexString(EmbeddedLoaderHash));
        }
        catch
        {
            return false;
        }
    }

    private static void WriteAllBytesProtected(string path, byte[] bytes)
    {
        if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
        File.WriteAllBytes(path, bytes);
    }

    public static void OpenInExplorer(string path)
        => SpeedhackManager.OpenInExplorer(path);
}

// ============================== 状态模型 ==============================

public sealed class ModStatus
{
    public bool BundleLoaderPresent { get; set; }
    public bool BundleConfigPresent { get; set; }
    public bool BundleSdkPresent { get; set; }
    public bool BundleSampleModPresent { get; set; }
    public string GameDirectory { get; set; } = "";
    public string AutoDetectedDirectory { get; set; } = "";
    public bool GameExeFound { get; set; }
    public bool GameRunning { get; set; }
    public bool Installed { get; set; }
    public bool LoaderPresent { get; set; }
    public bool LoaderMatchesBundle { get; set; }
    public string LoaderPath { get; set; } = "";
    public string LoaderRoot { get; set; } = "";
    public bool LoaderRootExists { get; set; }
    public string BundleHash { get; set; } = "";
    public string EmbeddedVersion { get; set; } = "";
    public string InstalledVersion { get; set; } = "";
    public string LatestVersion { get; set; } = "";
    public bool UpdateAvailable { get; set; }
    public string UpdateError { get; set; } = "";
    public string Message { get; set; } = "";
    public List<ModEntryInfo> Mods { get; set; } = new();
    public List<ModEntryInfo> Sdk { get; set; } = new();

    public string RunningInstallHint => ModManager.DescribeRunningInstall();
    public string RunningUninstallHint => ModManager.DescribeRunningUninstall();
}

public sealed class ModEntryInfo
{
    public string Name { get; set; } = "";
    public string FileName { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime ModifiedUtc { get; set; }

    /// <summary>sidecar 的 id（= 程序集名，依赖解析的 key）。</summary>
    public string Id { get; set; } = "";

    /// <summary>sidecar 声明的显示名(mod DLL 旁同名 .json 的 name 字段, 由 SDK 的 SdkManifest.ExportSidecar / cesium CLI 写出)。</summary>
    public string DisplayName { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>需要的 SDK 最低版本（API 版本协商用）。</summary>
    public string SdkVersion { get; set; } = "";

    /// <summary>mod 是否启用（sidecar enabled 字段, 缺省 true; false = 加载器跳过）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>权限位掩码（与 C# ModPermission 枚举一致: 1=ReadGameState 2=GameActions 4=SpeedHack 8=FileWrite）。</summary>
    public int Permissions { get; set; }

    /// <summary>依赖的其他 mod（id + 可选最低版本）。</summary>
    public List<ModDependencyInfo> Dependencies { get; set; } = new();
}

/// <summary>sidecar 里声明的依赖项。</summary>
public sealed class ModDependencyInfo
{
    public string Id { get; set; } = "";
    public string MinVersion { get; set; } = "";
}

/// <summary>CesiumLoader 发布包清单（cesium-loader.json）。</summary>
public sealed class LoaderPackageInfo
{
    public string Version { get; set; } = "";
    public Dictionary<string, string> Files { get; set; } = new();
}

/// <summary>mod 权限位（与 SDK 的 ModPermission 枚举一致）。</summary>
[Flags]
public enum ModPermission
{
    None = 0,
    ReadGameState = 1 << 0,
    GameActions = 1 << 1,
    SpeedHack = 1 << 2,
    FileWrite = 1 << 3,
}

/// <summary>加载器配置（doorstop_config.json）的可编辑字段。</summary>
public sealed class LoaderConfig
{
    public bool Enabled { get; set; } = true;
    public bool UseManagedBootstrap { get; set; }
    public string BootstrapAssembly { get; set; } = "CesiumLoader.Bootstrap.dll";
    public string BootstrapType { get; set; } = "CesiumLoader.Bootstrap.Bootstrap";
    public string BootstrapMethod { get; set; } = "Main";
    public int GameAssemblyTimeoutSec { get; set; } = 60;
    public int DomainTimeoutSec { get; set; } = 30;
    public int HybridclrTimeoutSec { get; set; } = 60;
    public bool ConsoleEnabled { get; set; } = true;
    public bool ConsoleTopmost { get; set; } = true;
    public bool ForwardActivityLog { get; set; } = true;
    public double SpeedhackBaseSpeed { get; set; } = 1.0;
    public string SdkVersion { get; set; } = "2.0.0";
}

/// <summary>mod 配置表单字段（configs\{mod}.json 键值编辑）。</summary>
public sealed class ModConfigField
{
    public string Name { get; set; } = "";
    /// <summary>bool / number / string / other（other 原样保留原始 JSON 文本）。</summary>
    public string Kind { get; set; } = "string";
    public bool BoolValue { get; set; }
    public double NumberValue { get; set; }
    public string StringValue { get; set; } = "";
}
