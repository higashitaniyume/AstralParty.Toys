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

    /// <summary>切换 mod 启用/禁用: 改写 sidecar(mods\{name}.json) 的 enabled 字段。
    /// 加载器读取 sidecar 的 enabled=false 时跳过该 mod。无 sidecar 的 mod 自动补一个。</summary>
    public bool ToggleMod(string gameDirectory, string fileName, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("游戏目录或 mod 文件名无效。");
        var modsDir = Path.Combine(gameDirectory, LoaderFolderName, ModsFolderName);
        var (dllPath, _) = ResolveModPath(modsDir, fileName);
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
        return enabled;
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

    /// <summary>按文件名在 mods 目录定位 mod(DLL 路径 + 文件夹名)。
    /// 新布局: mods\{FolderName}\{FolderName}.dll; 旧布局: mods\{FileName}.dll。</summary>
    private static (string? Dll, string? DirectoryName) ResolveModPath(string modsDir, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return (null, null);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        // 新布局优先
        var inFolder = Path.Combine(modsDir, stem, fileName);
        if (File.Exists(inFolder)) return (inFolder, stem);
        // 旧布局平铺
        var flat = Path.Combine(modsDir, fileName);
        if (File.Exists(flat)) return (flat, null);
        return (null, null);
    }

    /// <summary>从游戏 mods 目录删除一个 mod DLL。</summary>
    public ModEntryInfo? DeleteMod(string gameDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName))
            return null;
        var modsDir = Path.Combine(gameDirectory, LoaderFolderName, ModsFolderName);
        var (target, dirName) = ResolveModPath(modsDir, fileName);
        if (target is null || !File.Exists(target)) return null;

        var info = new ModEntryInfo
        {
            Name = Path.GetFileNameWithoutExtension(fileName),
            FileName = Path.GetFileName(target),
            DirectoryName = dirName,
            SizeBytes = new FileInfo(target).Length,
            ModifiedUtc = File.GetLastWriteTimeUtc(target)
        };
        try
        {
            File.Delete(target);
            // 新布局: 删掉整个 mod 文件夹(含 sidecar/附属文件); 平铺旧布局只删 DLL
            if (!string.IsNullOrEmpty(dirName))
            {
                var folder = Path.Combine(modsDir, dirName);
                if (Directory.Exists(folder))
                {
                    // 只删 mod 文件夹里的已知文件 + 空文件夹(不误删用户放在里面的其它东西? 约定: 整个文件夹都属于该 mod)
                    Directory.Delete(folder, recursive: true);
                }
            }
            return info;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
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
