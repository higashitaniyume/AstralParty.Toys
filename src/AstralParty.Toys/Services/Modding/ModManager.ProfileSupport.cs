using System;
using AstralParty.Toys.Models;

namespace AstralParty.Toys.Services.Modding;

/// <summary>模组方案 / 回滚日志需要的最小文件接口（不做版本探测、不算哈希，比 <see cref="ModManager.GetStatus"/> 轻）。</summary>
public sealed partial class ModManager
{
    /// <summary>列出 <c>mods</c> + <c>disabled_mods</c> 里的模组（启用的在前）。只读 sidecar 元数据。</summary>
    public IReadOnlyList<ModEntryInfo> GetInstalledMods(string gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
            return Array.Empty<ModEntryInfo>();
        var loaderRoot = Path.Combine(gameDirectory, LoaderFolderName);
        return ScanMods(Path.Combine(loaderRoot, ModsFolderName), Path.Combine(loaderRoot, DisabledModsFolderName));
    }

    /// <summary>模组 sidecar 的实际路径（从 DLL 位置推导，新布局在模组文件夹内）。模组不存在返回 null。</summary>
    public string? ModSidecarPath(string gameDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName)) return null;
        var loaderRoot = Path.Combine(gameDirectory, LoaderFolderName);
        var (dll, _, _) = ResolveModPath(Path.Combine(loaderRoot, ModsFolderName), fileName);
        return dll is null ? null : Path.ChangeExtension(dll, ".json");
    }

    /// <summary>mod 配置的落盘位置（<c>mods\{ModId}\config.json</c>；停用模组为它当前所在的目录，不要求文件已存在）。</summary>
    public string ModConfigWritePathFor(string gameDirectory, string fileName)
        => ModConfigWritePath(gameDirectory, fileName);

    /// <summary>
    /// 只搬模组目录（mods ↔ disabled_mods），**不改写 sidecar**。
    /// 回滚用：sidecar 随后按原文还原，不能被"写 enabled"顺手改掉。
    /// 返回 false = 模组不存在 / 目标已有同名目录 / 搬移失败。
    /// </summary>
    public bool SetModDirectoryEnabled(string gameDirectory, string fileName, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName)) return false;
        try
        {
            var loaderRoot = Path.Combine(gameDirectory, LoaderFolderName);
            var (dllPath, directoryName, root) = ResolveModPath(Path.Combine(loaderRoot, ModsFolderName), fileName);
            if (dllPath is null) return false;

            var targetRootName = enabled ? ModsFolderName : DisabledModsFolderName;
            var currentRootName = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.Equals(currentRootName, targetRootName, StringComparison.OrdinalIgnoreCase)) return true;

            MoveModEntry(loaderRoot, root, dllPath, directoryName, targetRootName);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
