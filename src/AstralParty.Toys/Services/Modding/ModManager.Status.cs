using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AstralParty.Toys.Models;
using AstralParty.Toys.Services.Common;
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
                    // 来源判定见 ClassifyLoaderOrigin：点「安装」装的是内置哈希，点「从 GitHub 更新」装的是
                    // 清单里记的哈希 —— 后者也是本工具装的，只是版本和本程序内置的不同。
                    status.LoaderOrigin = ClassifyLoaderOrigin(directory, dllPath);
                    status.LoaderManaged = status.LoaderOrigin.Length > 0;
                    status.Installed = true;
                }
            }

            status.GameRunning = SpeedhackManager.IsGameRunning();

            // 版本来自游戏安装清单；受管理的 DLL 在清单缺失时可使用本地安装历史。
            // 两份可靠记录都缺失时显示未知，不从日志或 SDK 版本猜加载器版本。
            status.InstalledVersion = status.Installed
                ? (status.LoaderManaged ? ReadCurrentInstallationVersion(directory) : ReadInstalledVersion(directory)) : "";
            status.EmbeddedVersion = EmbeddedVersion;
            status.InstalledSdkVersion = !string.IsNullOrEmpty(directory) && Directory.Exists(directory) &&
                                         ModManager.TryReadInstalledSdkVersion(directory, out var installedSdk)
                ? installedSdk
                : "";
            status.EmbeddedSdkVersion = EmbeddedSdkVersion;

            // mod 列表 / SDK 列表（安装目录存在才扫）
            var loaderRoot = string.IsNullOrEmpty(directory) ? "" : Path.Combine(directory, LoaderFolderName);
            status.LoaderRoot = loaderRoot;
            if (loaderRoot.Length > 0)
            {
                status.LoaderRootExists = Directory.Exists(loaderRoot);
                status.Mods = ScanMods(Path.Combine(loaderRoot, ModsFolderName), Path.Combine(loaderRoot, DisabledModsFolderName));
                status.Sdk = ScanMods(Path.Combine(loaderRoot, SdkFolderName));
            }

            ApplyRetainedStatus(status);
            status.BundleHash = EmbeddedLoaderHash ?? "";
            status.Message = BuildStatusMessage(status) + (status.GameRunning
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

    /// <summary>扫描 mod 目录。给了 <paramref name="disabledDirectory"/> 时把停用的 mod 一起列出来：
    /// 启用的排在前面，停用的强制 <see cref="ModEntryInfo.Enabled"/> = false 并标记来源目录。</summary>
    private static List<ModEntryInfo> ScanMods(string directory, string? disabledDirectory = null)
    {
        var list = ScanModsIn(directory, disabled: false);
        if (string.IsNullOrEmpty(disabledDirectory)) return list;
        foreach (var entry in ScanModsIn(disabledDirectory, disabled: true))
        {
            if (list.Any(m => string.Equals(m.Name, entry.Name, StringComparison.OrdinalIgnoreCase))) continue;
            list.Add(entry);
        }
        return list;
    }

    private static List<ModEntryInfo> ScanModsIn(string directory, bool disabled)
    {
        var list = new List<ModEntryInfo>();
        try
        {
            if (!Directory.Exists(directory)) return list;
            // 新布局: {ModId}\{ModId}.dll (每 mod 一个文件夹, sidecar 在文件夹内)
            foreach (var sub in Directory.EnumerateDirectories(directory).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var dirName = Path.GetFileName(sub);
                var dllPath = Path.Combine(sub, dirName + ".dll");
                if (!File.Exists(dllPath)) continue;   // 文件夹里没有同名 dll, 不是 mod 文件夹
                list.Add(ScanModEntry(dllPath, dirName, disabled));
            }
            // 旧布局兼容: 根下平铺的 .dll (sidecar 在同级)
            foreach (var file in Directory.EnumerateFiles(directory, "*.dll").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                if (list.Any(m => string.Equals(m.Name, Path.GetFileNameWithoutExtension(file), StringComparison.OrdinalIgnoreCase)))
                    continue;
                list.Add(ScanModEntry(file, null, disabled));
            }
        }
        catch
        {
            // 扫描失败按空列表处理
        }
        return list;
    }

    private static ModEntryInfo ScanModEntry(string file, string? directoryName, bool disabled = false)
    {
        var info = new FileInfo(file);
        var entry = new ModEntryInfo
        {
            Name = Path.GetFileNameWithoutExtension(file),
            FileName = Path.GetFileName(file),
            DirectoryName = directoryName,
            SizeBytes = info.Length,
            ModifiedUtc = info.LastWriteTimeUtc,
            DisabledFolder = disabled
        };
        // 同名 sidecar(新布局: mod 文件夹内 {Name}.json; 旧布局: 同目录 {Name}.json):
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
        return entry;
    }

}
