namespace AstralParty.Toys.Services.Common;

/// <summary>
/// mod 玩家配置的位置。与加载器 SDK 的 <c>ModConfig.ResolvePath</c> 保持同一套规则：
///
///  - 现行位置：<c>&lt;游戏目录&gt;\AstralParty_ModLoader\configs\{ModId}.json</c>，存在就用它；
///  - 模组目录里的 <c>mods\{ModId}\config.json</c> 是随包默认值，只有 configs 下没有玩家配置时才会被读到。
///
/// <b>写入必须落在 configs</b>：一旦 configs 下已有文件，mod 就只读那份，写到模组目录等于没保存
/// （音效 / 卡牌皮肤 / 角色皮肤方案与模组配置弹窗都踩过这个坑）。
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

    /// <summary>现行玩家配置路径；只拼接，不要求已存在。写入目标一律用它。</summary>
    public static string Canonical(string gameDirectory, string modId) =>
        Path.Combine(ConfigsDirectory(gameDirectory), modId + ".json");

    /// <summary>模组目录里的随包默认配置（旧安装的玩家配置也可能还停在这里）。</summary>
    public static string Packaged(string gameDirectory, string modId) =>
        Path.Combine(LoaderDirectory(gameDirectory), "mods", modId, "config.json");

    /// <summary>读取用：configs 下有就返回它，否则回退模组目录那份；两处都没有时返回 configs 路径。</summary>
    public static string ResolveForRead(string gameDirectory, string modId)
    {
        var canonical = Canonical(gameDirectory, modId);
        if (File.Exists(canonical)) return canonical;
        var packaged = Packaged(gameDirectory, modId);
        return File.Exists(packaged) ? packaged : canonical;
    }

    /// <summary>读取用：两处都没有时返回 null（调用方可以据此决定是否用默认值）。</summary>
    public static string? ResolveExisting(string gameDirectory, string modId)
    {
        var canonical = Canonical(gameDirectory, modId);
        if (File.Exists(canonical)) return canonical;
        var packaged = Packaged(gameDirectory, modId);
        return File.Exists(packaged) ? packaged : null;
    }
}
