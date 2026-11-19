using AstralParty.Toys.Models;
using AstralParty.Toys.Services.Speedhack;

namespace AstralParty.Toys.Services.Modding;

public sealed partial class ModManager
{
    // ============================== 状态 ==============================

    public ModStatus GetStatus()
    {
        var status = new ModStatus();
        PruneHistoryIfDue();   // 每 6 小时最多一次：install-history 不无限长
        try
        {
            var directory = GetStoredGameDirectory();
            var autoDetectedDirectory = "";
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                directory = autoDetectedDirectory = SpeedhackManager.DetectGameDirectory() ?? "";
            var exeFound = directory.Length > 0 && SpeedhackManager.ContainsGameExe(directory) is not null;
            status = ReadDiskStatus(directory, exeFound, SpeedhackManager.IsGameRunning(), out var manifest);
            status.AutoDetectedDirectory = autoDetectedDirectory;
            status.BundleLoaderPresent = HasEmbeddedLoader;
            status.BundleConfigPresent = HasEmbeddedConfig;
            status.BundleSdkPresent = HasEmbeddedSdk;
            status.BundleSampleModPresent = HasEmbeddedSampleMod;
            status.BundleHash = EmbeddedLoaderHash ?? "";
            status.EmbeddedVersion = EmbeddedVersion;
            status.EmbeddedSdkVersion = EmbeddedSdkVersion;

            var retained = directory.Length > 0 ? ReadRetainedInstallation(directory) : null;
            if (status.LoaderPresent)
            {
                status.LoaderOrigin = ClassifyLoaderOrigin(directory, status.LoaderPath, manifest, retained);
                status.LoaderManaged = status.LoaderOrigin.Length > 0;
                status.LoaderMatchesBundle = status.LoaderOrigin == "bundle";
                if (status.LoaderManaged && status.InstalledVersion.Length == 0 && retained is { Removed: false })
                    status.InstalledVersion = retained.Version;
            }

            ApplyRetainedStatus(status, manifest, retained);
            status.Message = BuildStatusMessage(status) + (status.GameRunning == true
                ? " 游戏正在运行：安装、更新、停用或恢复要重启游戏才生效，version.dll 被占用时请完全退出游戏再操作。" : "");
        }
        catch (Exception ex)
        {
            status.Message = $"读取状态失败：{ex.Message}";
        }
        return status;
    }

    /// <summary>状态栏那一句话。只讲「现在是什么状态、下一步该做什么」，不再复述界面已经分栏显示的版本号。</summary>
    private string BuildStatusMessage(ModStatus status)
    {
        if (!HasEmbeddedLoader)
            return "程序集缺少内置加载器资源（ModLoader.version.dll），请重新编译发布版本。";
        if (string.IsNullOrEmpty(status.GameDirectory))
            return "尚未找到游戏目录：点「自动检测」，或手动选择游戏 exe 所在的文件夹。";
        if (!Directory.Exists(status.GameDirectory))
            return $"游戏目录不存在：{status.GameDirectory}";
        if (status.LoaderMissing)
            return status.CanRestoreLoader
                ? "version.dll 已停用或缺失，模组与配置已保留；点击恢复，缺少本地备份时会下载当前版本的加载器包。"
                : "version.dll 缺失且版本未知，请重新安装或更新完整加载器。";
        if (!status.Installed)
            return "尚未安装：点「安装加载器」，把加载器和内置模组装进游戏目录。";

        var mods = status.Mods.Count(m => m.Enabled) is var enabledCount && enabledCount == status.Mods.Count
            ? $"已加载 {enabledCount} 个模组"
            : $"已加载 {enabledCount} 个模组（另有 {status.Mods.Count - enabledCount} 个停用）";
        if (!status.LoaderManaged)
            return $"游戏目录里的 version.dll 不是本工具装的（来源不明）——可能是旧版独立变速器或第三方文件，安装或卸载前请先确认。";
        if (status.InstalledVersion.Length == 0)
            return $"加载器已就位，但这份安装没有版本清单 —— 重新安装一次就能纳入版本管理（{mods}）。";
        return string.Equals(status.InstalledVersion, status.EmbeddedVersion, StringComparison.OrdinalIgnoreCase)
            ? $"加载器已就绪，与内置版本一致（{mods}）。"
            : $"加载器已就绪（{mods}）。";
    }
}
