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
    // ============================== 游戏变速（加载器内置功能） ==============================

    /// <summary>设置加载器内置变速的基础倍率: 写 doorstop_config.json 的 speedhackBaseSpeed。
    /// 1.0 = 正常(禁用变速); 其他值(如 2.0) = 启动游戏即变速, 全程保持。重启游戏生效。
    /// 下限硬性为 1.0 —— 加载器不接受减速请求(低于 1.0 会被忽略并按 1.0 处理)。</summary>
    public void SetSpeedhack(string gameDirectory, double baseSpeed)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory))
            throw new ArgumentException("请先选择游戏目录。");
        if (baseSpeed < MinBaseSpeed || baseSpeed > MaxBaseSpeed)
            throw new InvalidOperationException(
                $"倍速必须在 [{MinBaseSpeed:0.#},{MaxBaseSpeed:0.#}] 之间（1.0 = 正常速度，不支持减速）。");

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

    /// <summary>切换 mod 启用/禁用。禁用 = 把 mod 搬进 <c>disabled_mods</c>（加载器只扫 mods，DLL 也不会被游戏占用），
    /// 启用 = 搬回 <c>mods</c>；sidecar 的 enabled 字段同步改写，手动挪回去时也还是同样状态。
    /// 无 sidecar 的 mod 自动补一个。</summary>
    public bool ToggleMod(string gameDirectory, string fileName, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("游戏目录或 mod 文件名无效。");
        var loaderRoot = Path.Combine(gameDirectory, LoaderFolderName);
        var (dllPath, directoryName, root) = ResolveModPath(Path.Combine(loaderRoot, ModsFolderName), fileName);
        if (dllPath is null) throw new FileNotFoundException($"mod 不存在：{fileName}");

        var sidecarPath = Path.ChangeExtension(dllPath, ".json");
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

        // 目录搬家（先写 sidecar 再搬，搬失败也不会留下状态不一致的 sidecar）
        var targetRootName = enabled ? ModsFolderName : DisabledModsFolderName;
        var currentRootName = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!string.Equals(currentRootName, targetRootName, StringComparison.OrdinalIgnoreCase))
            MoveModEntry(loaderRoot, root, dllPath, directoryName, targetRootName);
        ToysLog.Info("mods",
            $"{(enabled ? "启用" : "禁用")}模组 {fileName}" + (enabled ? "" : "（已搬入 disabled_mods）"));
        return enabled;
    }

    /// <summary>把 mod 从当前根目录搬到另一个根目录（mod 文件夹整体搬；旧布局平铺的 DLL 连同 sidecar 一起搬）。</summary>
    private static void MoveModEntry(string loaderRoot, string sourceRoot, string dllPath, string? directoryName, string targetRootName)
    {
        var targetRoot = Path.Combine(loaderRoot, targetRootName);
        try
        {
            Directory.CreateDirectory(targetRoot);
            if (!string.IsNullOrEmpty(directoryName))
            {
                var target = Path.Combine(targetRoot, directoryName);
                if (Directory.Exists(target))
                    throw new IOException($"目标目录已存在同名模组：{target}");
                Directory.Move(Path.Combine(sourceRoot, directoryName), target);
                return;
            }

            foreach (var path in new[] { dllPath, Path.ChangeExtension(dllPath, ".json") })
            {
                if (!File.Exists(path)) continue;
                var target = Path.Combine(targetRoot, Path.GetFileName(path));
                if (File.Exists(target))
                    throw new IOException($"目标目录已存在同名文件：{target}");
                File.Move(path, target);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"移动 mod 到 {targetRootName} 失败：{ex.Message}（游戏运行时 DLL 可能被占用）。", ex);
        }
    }

    /// <summary>批量切换 mod 启用/禁用：逐个调用 <see cref="ToggleMod"/>，单项失败不中断其余项。</summary>
    /// <returns>成功数与失败的文件名（空/空白会被忽略）。</returns>
    public (int Succeeded, string[] Failed) ToggleMods(string gameDirectory, IEnumerable<string> fileNames, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory)) throw new ArgumentException("游戏目录无效。");
        var names = (fileNames ?? Enumerable.Empty<string>())
            .Where(f => !string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var failed = new List<string>();
        int ok = 0;
        foreach (var fileName in names)
        {
            try { ToggleMod(gameDirectory, fileName, enabled); ok++; }
            catch (Exception) { failed.Add(fileName); }
        }
        return (ok, failed.ToArray());
    }

    /// <summary>按文件名定位 mod（DLL 路径 + 文件夹名 + 所在根目录）。
    /// 新布局: {root}\{FolderName}\{FolderName}.dll; 旧布局: {root}\{FileName}.dll。
    /// 先找 <c>mods</c>（启用的），再找同级的 <c>disabled_mods</c>（停用的）。</summary>
    private static (string? Dll, string? DirectoryName, string Root) ResolveModPath(string modsDir, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return (null, null, modsDir);
        var loaderRoot = Path.GetDirectoryName(modsDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                         ?? modsDir;
        var stem = Path.GetFileNameWithoutExtension(fileName);
        foreach (var root in new[] { modsDir, Path.Combine(loaderRoot, DisabledModsFolderName) })
        {
            // 新布局优先
            var inFolder = Path.Combine(root, stem, fileName);
            if (File.Exists(inFolder)) return (inFolder, stem, root);
            // 旧布局平铺
            var flat = Path.Combine(root, fileName);
            if (File.Exists(flat)) return (flat, null, root);
        }
        return (null, null, modsDir);
    }

    /// <summary>从游戏 mods / disabled_mods 目录删除一个 mod（含整个模组文件夹）。</summary>
    public ModEntryInfo? DeleteMod(string gameDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName))
            return null;
        var loaderRoot = Path.Combine(gameDirectory, LoaderFolderName);
        var (target, dirName, root) = ResolveModPath(Path.Combine(loaderRoot, ModsFolderName), fileName);
        if (target is null || !File.Exists(target)) return null;

        var info = new ModEntryInfo
        {
            Name = Path.GetFileNameWithoutExtension(fileName),
            FileName = Path.GetFileName(target),
            DirectoryName = dirName,
            SizeBytes = new FileInfo(target).Length,
            ModifiedUtc = File.GetLastWriteTimeUtc(target),
            DisabledFolder = string.Equals(
                Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                DisabledModsFolderName, StringComparison.OrdinalIgnoreCase)
        };
        try
        {
            File.Delete(target);
            // 新布局: 删掉整个 mod 文件夹(含 sidecar/附属文件); 平铺旧布局只删 DLL + sidecar
            if (!string.IsNullOrEmpty(dirName))
            {
                var folder = Path.Combine(root, dirName);
                if (Directory.Exists(folder))
                {
                    // 只删 mod 文件夹里的已知文件 + 空文件夹(不误删用户放在里面的其它东西? 约定: 整个文件夹都属于该 mod)
                    Directory.Delete(folder, recursive: true);
                }
            }
            else
            {
                var sidecar = Path.ChangeExtension(target, ".json");
                if (File.Exists(sidecar)) File.Delete(sidecar);
            }
            ToysLog.Info("mods", $"删除模组 {info.Name}" + (info.DisabledFolder ? "（原在 disabled_mods）" : ""));
            return info;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ToysLog.Error("mods", $"删除模组 {fileName} 失败", ex);
            throw new IOException($"删除 {fileName} 失败：{ex.Message}（游戏运行时 DLL 可能被占用）。", ex);
        }
    }

    /// <summary>批量删除 mod：逐个调用 <see cref="DeleteMod"/>，单项失败不中断其余项。</summary>
    /// <returns>成功数与失败的文件名（空/空白会被忽略，未找到也算失败）。</returns>
    public (int Succeeded, string[] Failed) DeleteMods(string gameDirectory, IEnumerable<string> fileNames)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory)) throw new ArgumentException("游戏目录无效。");
        var names = (fileNames ?? Enumerable.Empty<string>())
            .Where(f => !string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var failed = new List<string>();
        int ok = 0;
        foreach (var fileName in names)
        {
            try
            {
                if (DeleteMod(gameDirectory, fileName) is not null) ok++;
                else failed.Add(fileName);
            }
            catch (Exception) { failed.Add(fileName); }
        }
        return (ok, failed.ToArray());
    }

}
