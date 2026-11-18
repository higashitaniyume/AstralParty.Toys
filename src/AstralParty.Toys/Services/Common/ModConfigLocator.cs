using System.Text;

namespace AstralParty.Toys.Services.Common;

/// <summary>
/// mod 玩家配置的位置。与加载器 SDK 的 <c>ModConfig.ResolvePath</c> 保持同一套规则：
///
///  - 现行位置：<c>&lt;游戏目录&gt;\AstralParty_ModLoader\configs\{ModId}.json</c>，存在就用它；
///  - 模组目录里的 <c>mods\{ModId}\config.json</c> 是随包默认值，只有 configs 下没有玩家配置时才会被读到。
///
/// <b>写入两份</b>：configs 是加载器优先读的位置，模组目录那份仍有个别模组（如 CharacterSkinMod）
/// 直接按路径读取，所以每次改动都要同时写一次，保证两边一致。
/// </summary>
public static class ModConfigLocator
{
    public const string LoaderFolderName = "AstralParty_ModLoader";

    /// <summary>加载器目录（AstralParty_ModLoader）。</summary>
    public static string LoaderDirectory(string gameDirectory) =>
        Path.Combine(gameDirectory, LoaderFolderName);

    /// <summary>玩家配置目录（configs）。</summary>
    public static string ConfigsDirectory(string gameDirectory) =>
        Path.Combine(LoaderDirectory(gameDirectory), "configs");

    /// <summary>启用的模组目录（mods）。</summary>
    public static string ModsDirectory(string gameDirectory) =>
        Path.Combine(LoaderDirectory(gameDirectory), "mods");

    /// <summary>停用的模组目录（disabled_mods）。</summary>
    public static string DisabledModsDirectory(string gameDirectory) =>
        Path.Combine(LoaderDirectory(gameDirectory), "disabled_mods");

    /// <summary>现行玩家配置路径；只拼接，不要求已存在。写入目标一律用它。</summary>
    public static string Canonical(string gameDirectory, string modId) =>
        Path.Combine(ConfigsDirectory(gameDirectory), modId + ".json");

    /// <summary>模组目录里的随包默认配置（旧安装的玩家配置也可能还停在这里）。</summary>
    public static string Packaged(string gameDirectory, string modId) =>
        Path.Combine(ModsDirectory(gameDirectory), modId, "config.json");

    /// <summary>
    /// 模组目录里那份 <c>config.json</c>；模组没装在 <c>mods</c> 或 <c>disabled_mods</c> 时返回 null
    /// （不凭空建一个没有 DLL 的模组目录）。停用的模组写回它当前所在的目录，不把它搬回 <c>mods</c>。
    /// </summary>
    public static string? PackagedIfInstalled(string gameDirectory, string modId)
    {
        foreach (var root in new[] { ModsDirectory(gameDirectory), DisabledModsDirectory(gameDirectory) })
        {
            var directory = Path.Combine(root, modId);
            if (Directory.Exists(directory)) return Path.Combine(directory, "config.json");
        }
        return null;
    }

    /// <summary>读取用：configs 下有就返回它，否则回退模组目录那份；两处都没有时返回 configs 路径。</summary>
    public static string ResolveForRead(string gameDirectory, string modId)
    {
        var canonical = Canonical(gameDirectory, modId);
        if (File.Exists(canonical)) return canonical;
        var packaged = PackagedIfInstalled(gameDirectory, modId) ?? Packaged(gameDirectory, modId);
        return File.Exists(packaged) ? packaged : canonical;
    }

    /// <summary>
    /// 写入目标：configs 一定写；模组装在 <c>mods</c> / <c>disabled_mods</c> 时，模组目录那份也写一次。
    /// 第一个元素始终是 configs 路径。
    /// </summary>
    public static IReadOnlyList<string> WriteTargets(string gameDirectory, string modId)
    {
        var targets = new List<string> { Canonical(gameDirectory, modId) };
        var packaged = PackagedIfInstalled(gameDirectory, modId);
        if (packaged is not null) targets.Add(packaged);
        return targets;
    }

    /// <summary>按字节写入所有目标（临时文件 + 替换）；返回 configs 路径。</summary>
    public static string WriteAllBytes(string gameDirectory, string modId, byte[] bytes)
    {
        var targets = WriteTargets(gameDirectory, modId);
        foreach (var path in targets) WriteAtomic(path, bytes);
        return targets[0];
    }

    /// <summary>按文本写入所有目标（默认 UTF-8 无 BOM；<paramref name="bom"/> 为 true 时保留 BOM）；返回 configs 路径。</summary>
    public static string WriteText(string gameDirectory, string modId, string text, bool bom = false)
        => WriteAllBytes(gameDirectory, modId, new UTF8Encoding(bom).GetBytes(text));

    private static void WriteAtomic(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        try
        {
            File.WriteAllBytes(temp, bytes);
            if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
            File.Move(temp, path, true);
        }
        catch
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            throw;
        }
    }
}
