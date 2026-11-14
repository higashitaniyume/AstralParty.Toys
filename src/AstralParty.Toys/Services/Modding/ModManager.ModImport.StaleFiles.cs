using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AstralParty.Toys.Models;
using AstralParty.Toys.Services.Common;

namespace AstralParty.Toys.Services.Modding;

public sealed partial class ModManager
{
    // ============================== 升级：旧版本残留 ==============================

    /// <summary>
    /// 升级前算出会被清掉的旧文件。清单（<see cref="SaveModPackageManifest"/>）存在时只清清单里记过、
    /// 新包已经没有的文件 —— 玩家后来自己放进模组文件夹的东西不动；没有清单的手动安装（本功能之前装的）
    /// 只清 dll / pdb / json / xml 这类构建产物。config.json 永远保留。
    /// </summary>
    private List<string> FindStaleModFiles(string gameDirectory, PackageModPlan plan, string modFolder)
    {
        var stale = new List<string>();
        if (!Directory.Exists(modFolder)) return stale;
        var manifest = ReadModPackageManifest(gameDirectory, plan.ModId);
        var installedFiles = new HashSet<string>(
            plan.Files.Select(plan.RelativePathOf), StringComparer.OrdinalIgnoreCase);
        foreach (var relative in EnumerateModFolderFiles(modFolder))
        {
            if (relative.Equals(ModConfigFileName, StringComparison.OrdinalIgnoreCase)) continue;
            if (installedFiles.Contains(relative)) continue;
            var owned = manifest is null
                ? ModPackageArtifactExtensions.Contains(Path.GetExtension(relative))
                : manifest.Contains(relative);
            if (owned) stale.Add(relative);
        }
        return stale;
    }

    /// <summary>
    /// 删掉计划里标记的旧版本残留。文件走 Windows 回收站（和资源管理器里删除一样，可以还原），
    /// **绝不永久删除**：卷上没有回收站或文件被占用时，旧文件原样保留并记进 <see cref="ModImportResult.KeptFiles"/>。
    /// </summary>
    private void RemoveStaleModFiles(string modFolder, PackageModPlan plan, ModImportResult result)
    {
        var recycle = RecycleFileHook ?? RecycleBinHelper.TryRecycleFile;
        foreach (var relative in plan.StaleFiles)
        {
            var target = Path.Combine(modFolder, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!IsInside(modFolder, target) || !File.Exists(target)) continue;
            var reported = Path.Combine(plan.FolderName, relative).Replace('\\', '/');
            var (recycled, _) = recycle(target);
            if (recycled) result.RemovedFiles.Add(reported);
            else result.KeptFiles.Add(reported);
        }
        RemoveEmptyDirectories(modFolder);
    }

    /// <summary>递归删掉空子目录（新版本不再带的资源目录别留一堆空文件夹）。</summary>
    private static void RemoveEmptyDirectories(string root)
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                         .OrderByDescending(path => path.Length))
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
            }
        }
        catch
        {
            // 清理空目录失败不影响使用
        }
    }

    private static IEnumerable<string> EnumerateModFolderFiles(string modFolder)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(modFolder, "*", SearchOption.AllDirectories).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
        return files.Select(file => Path.GetRelativePath(modFolder, file).Replace('\\', '/'));
    }

    /// <summary>
    /// 上次导入写了哪些文件（放在配置根的安装历史里，不污染 mods 目录）。
    /// 升级时只清这份清单里记过、新包已经没有的条目，玩家后加的文件不受影响。
    /// </summary>
    private string ModPackageManifestPath(string gameDirectory, string modId)
        => Path.Combine(GameHistoryDirectory(gameDirectory), "packages", modId + ".json");

    private HashSet<string>? ReadModPackageManifest(string gameDirectory, string modId)
    {
        try
        {
            var path = ModPackageManifestPath(gameDirectory, modId);
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
                return null;
            return new HashSet<string>(
                files.EnumerateArray().Select(element => element.GetString() ?? "").Where(file => file.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return null;
        }
    }

    private void SaveModPackageManifest(string gameDirectory, string modId, IEnumerable<string> files)
    {
        try
        {
            var path = ModPackageManifestPath(gameDirectory, modId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(
                new { files = files.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // 清单只服务于「下次升级清残留」：写不了不影响本次安装
        }
    }

    // ============================== 小工具 ==============================

    private static string ReadTextOrEmpty(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>从 JSON 文本里读一个字符串字段（容忍注释与 BOM）；读不到返回空串，不猜。</summary>
    private static string ReadJsonString(string json, string field)
    {
        if (string.IsNullOrWhiteSpace(json)) return "";
        try
        {
            using var document = JsonDocument.Parse(StripJsonComments(json.TrimStart('\uFEFF')));
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty(field, out var element)
                   && element.ValueKind == JsonValueKind.String
                ? element.GetString() ?? ""
                : "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>沿用已有的文件夹写法（Windows 不区分大小写：已存在 <c>mods\handviewermod</c> 时用它），
    /// 避免同一个模组出现两个只差大小写的目录。停用目录也一起看。</summary>
    private static string ResolveModFolderName(string modsDir, string modId, string? disabledDir = null)
    {
        foreach (var root in disabledDir is null ? new[] { modsDir } : new[] { modsDir, disabledDir })
        {
            try
            {
                foreach (var directory in Directory.EnumerateDirectories(root))
                {
                    var name = Path.GetFileName(directory);
                    if (name.Equals(modId, StringComparison.OrdinalIgnoreCase)) return name;
                }
            }
            catch
            {
                // 扫描失败就用请求的名字
            }
        }
        return modId;
    }

    /// <summary>sidecar 里的 enabled 字段：true / false / null（没有 sidecar 或不认识）。</summary>
    private static bool? ReadSidecarEnabled(string sidecarPath)
    {
        try
        {
            if (!File.Exists(sidecarPath)) return null;
            using var document = JsonDocument.Parse(StripJsonComments(File.ReadAllText(sidecarPath)));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!document.RootElement.TryGetProperty("enabled", out var enabled)) return null;
            return enabled.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary><paramref name="target"/> 是否确实位于 <paramref name="root"/> 之内（防 zip 路径穿越）。</summary>
    private static bool IsInside(string root, string target)
    {
        try
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
                           + Path.DirectorySeparatorChar;
            return Path.GetFullPath(target).StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
