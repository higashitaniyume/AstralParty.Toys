using System.Text.Json;
using AstralParty.Toys.Models;
using AstralParty.Toys.Services.App;
using AstralParty.Toys.Services.Common;
using AstralParty.Toys.Services.Game;
using AstralParty.Toys.Services.Modding;

namespace AstralParty.Toys.LinuxHost;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static int Main()
    {
        try
        {
            using var input = Console.OpenStandardInput();
            var request = JsonSerializer.Deserialize<HostRequest>(input, Json)
                ?? throw new InvalidDataException("Linux 宿主未收到有效请求。");
            var directory = request.GameDirectory ?? "";
            if (directory.Length > 0 && (!Path.IsPathFullyQualified(directory) || request.Edition is not ("cn" or "global")))
                throw new InvalidDataException("宿主提供的游戏位置或区服无效。");

            IEnumerable<object> events = request.Type switch
            {
                "ready" or "modStatus" or "gameProfilesGet" or "modProfilesGet" or "getAppVersion" =>
                    ReadOverview(request, directory),
                "linuxInstallLoader" or "linuxUpdateLoader" or "linuxUninstallLoader" => Apply(request, directory),
                _ => throw new InvalidOperationException("此操作在 Linux 宿主中不可用。")
            };
            Console.WriteLine(JsonSerializer.Serialize(events, Json));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static object[] Apply(HostRequest request, string directory)
    {
        if (directory.Length == 0) throw new InvalidOperationException("请先选择 Steam 游戏位置。");
        if (request.GameRunning != false) throw new InvalidOperationException("确认游戏已完全退出后才能修改加载器。");
        LinuxLoader.Apply(request.Type, directory);
        // The native host performs the refresh separately. A read failure must not undo a committed registry change.
        return [new { type = "toast", message = "CesiumLoader 操作已完成。" }];
    }

    private static List<object> ReadOverview(HostRequest request, string directory)
    {
        var profiles = (request.GameDirectories ?? []).Select(profile => DescribeGame(profile, directory)).ToArray();
        var active = profiles.FirstOrDefault(profile => profile.Active);
        var exeFound = active?.Valid ?? (FindExecutable(directory, request.Edition) is not null);
        var status = ModManager.ReadDiskStatus(directory, exeFound, request.GameRunning, out var manifest);
        var bundle = LinuxLoader.DescribeBundle();
        status.BundleLoaderPresent = bundle.LoaderPresent;
        status.BundleConfigPresent = bundle.ConfigPresent;
        status.BundleSdkPresent = bundle.SdkPresent;
        status.BundleSampleModPresent = bundle.Mods.Contains("ActivityLogMod", StringComparer.Ordinal);
        status.EmbeddedVersion = bundle.Version;
        status.EmbeddedSdkVersion = bundle.SdkVersion;

        var warnings = new List<string>(3);
        if (directory.Length > 0)
        {
            var proxyCollision = InspectionFiles.IsSymbolicLink(status.LoaderPath) || InspectionFiles.DirectoryExists(status.LoaderPath);
            if (proxyCollision) status.LoaderPresent = true;
            var manifestPath = Path.Combine(status.LoaderRoot, ModManager.InstalledManifestFileName);
            var ownsManifest = manifest.LoaderHash.Length > 0 && LinuxLoader.HasManagedManifest(directory);
            status.LoaderManaged = ownsManifest && (!status.LoaderPresent ||
                !proxyCollision && ModManager.MatchesHash(status.LoaderPath, manifest.LoaderHash));
            if (status.LoaderManaged)
            {
                status.LoaderOrigin = "manifest";
                status.InstalledVersion = manifest.Version;
                status.LoaderMissing = !status.LoaderPresent;
                status.LoaderDisabled = status.LoaderMissing;
            }
            if (proxyCollision || InspectionFiles.IsSymbolicLink(status.LoaderRoot))
                warnings.Add("加载器路径包含符号链接或不是预期的文件类型；未读取这些路径。");
            else if (!ownsManifest && (InspectionFiles.FileExists(manifestPath) || InspectionFiles.IsSymbolicLink(manifestPath)))
                warnings.Add("无法验证 CesiumLoader 的安装清单，不会管理其中记录的文件。");
            if (request.GameRunning is null)
                warnings.Add("无法确认游戏是否已退出，加载器文件操作不可用。");
        }

        var configPath = ModManager.LoaderConfigFilePath(directory);
        var configExists = directory.Length > 0 && InspectionFiles.FileExists(configPath);
        LoaderConfig? loaderConfig = null;
        bool? steamBypass = null;
        if (configExists && ModManager.TryReadLoaderConfig(directory, out var config, out steamBypass))
        {
            loaderConfig = config;
            if (status.LoaderManaged && !config.Enabled) status.LoaderDisabled = true;
            if (steamBypass == true || ModManager.IsTapTapBypassActive(config))
                warnings.Add("加载器配置启用了绕过选项。Linux 宿主只支持常规 Steam 启动，不会更改这些设置。");
        }
        if (configExists && steamBypass is null)
            warnings.Add("无法从加载器配置确认 Steam 绕过状态。");

        var (latestLog, summary, logWarning) = LoaderLog.ReadLatest(status.LoaderRoot);
        if (logWarning.Length > 0) warnings.Add(logWarning);
        status.Message = DescribeStatus(status);

        // Native overview does not request portraits, account data, replay data, or alternate launch modes.
        var events = new List<object>(6)
        {
            new { type = "appVersion", payload = AppInfo.Get(status) },
            new { type = "gameProfiles", payload = new { profiles, activeId = active?.Id } },
            new { type = "modStatus", payload = new { status, builtInMods = bundle.Mods } },
            new { type = "linuxStatus", payload = new
            {
                latestLog, summary, gameRunning = request.GameRunning, warning = string.Join('\n', warnings),
                availableLoaderVersion = bundle.Version
            } },
            ReadModProfiles(request.DataDirectory, status, configExists, loaderConfig)
        };
        if (steamBypass.HasValue)
            events.Add(new { type = "steamBypassSync", payload = new { enabled = steamBypass.Value } });
        return events;
    }

    private static string DescribeStatus(ModStatus status)
    {
        if (status.GameDirectory.Length == 0) return "尚未找到 Steam 安装的吉星派对。";
        if (!status.GameExeFound) return "所选游戏主程序已不存在，请重新检测。";
        if (status.LoaderPresent && !status.LoaderManaged) return "version.dll 来源不明或与安装清单不符。";
        if (status.LoaderMissing) return "version.dll 缺失，安装清单和已有模组已保留。";
        if (status.LoaderDisabled) return "加载器配置已停用。";
        if (!status.Installed) return "未安装 CesiumLoader。";
        var enabled = status.Mods.Count(mod => mod.Enabled && !mod.DisabledFolder);
        return $"检测到 CesiumLoader 和 {status.Mods.Count} 个模组（配置启用 {enabled} 个）。";
    }

    private static GameProfileView DescribeGame(GameProfile profile, string directory)
    {
        profile.Label = GameLibraryService.DefaultLabel(profile.Edition, profile.Directory);
        var exists = InspectionFiles.DirectoryExists(profile.Directory);
        var executable = exists ? FindExecutable(profile.Directory, profile.Edition) : null;
        return GameLibraryService.DescribeProfile(profile, string.Equals(profile.Directory, directory, StringComparison.Ordinal),
            exists, executable is not null, executable);
    }

    private static string? FindExecutable(string directory, string? edition)
    {
        if (directory.Length == 0) return null;
        return edition switch
        {
            "cn" when InspectionFiles.FileExists(Path.Combine(directory, "AstralParty_CN.exe")) => "AstralParty_CN.exe",
            "global" when InspectionFiles.FileExists(Path.Combine(directory, "AstralParty_INT.exe")) => "AstralParty_INT.exe",
            "global" when InspectionFiles.FileExists(Path.Combine(directory, "AstralParty.exe")) => "AstralParty.exe",
            _ => null
        };
    }

    private static object ReadModProfiles(string? dataDirectory, ModStatus status, bool configExists, LoaderConfig? loaderConfig)
    {
        var directory = status.GameDirectory;
        var path = dataDirectory is not null && Path.IsPathFullyQualified(dataDirectory) ? ModProfileService.GetStorePath(dataDirectory) : null;
        var store = path is null ? null : ModProfileService.ReadStore(path);
        IReadOnlyList<ModProfileSummary> profiles = store is null ? [] : ModProfileService.Summarize(store, directory, status.Mods, loaderConfig);
        var error = store is null && path is not null && (InspectionFiles.FileExists(path) || InspectionFiles.IsSymbolicLink(path))
            ? "模组方案文件无法读取。"
            : directory.Length == 0 ? "还没有选择游戏目录。" : "";
        return new { type = "modProfiles", payload = new
        {
            gameDirectory = directory, activeName = store?.Active ?? "",
            profiles,
            loaderInstalled = configExists, loaderMissing = directory.Length > 0 && !status.LoaderRootExists,
            gameRunning = status.GameRunning, namePattern = ModProfileService.NamePattern,
            nameMaxLength = ModProfileService.MaxNameLength, maxNoteLength = ModProfileService.MaxNoteLength, error
        } };
    }

    private sealed record HostRequest(string Type, string? GameDirectory, GameProfile[]? GameDirectories, bool? GameRunning,
        string? DataDirectory, string? Edition);
}
